using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;

using IronPython.Hosting;
using IronPython.Modules;
using IronPython.Runtime;
using IronPython.Runtime.Calls;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;

using Microsoft.Scripting;
using Microsoft.Scripting.Hosting;
using Microsoft.Scripting.Runtime;

using Ironclad.Structs;

namespace Ironclad
{
    public interface IAllocator
    {
        IntPtr Alloc(int bytes);
        IntPtr Realloc(IntPtr old, int bytes);
        void Free(IntPtr address);
        void FreeAll();
    }
    
    public class HGlobalAllocator : StupidSet, IAllocator
    {
        // in a desperate attempt to work around non-deterministic GC, in
        // which our members may be finalized before we are, we inherit from
        // StupidSet instead of just incorporating one.
        
        ~HGlobalAllocator()
        {
            this.FreeAll();
        }
        
        public virtual IntPtr 
        Alloc(int bytes)
        {
            IntPtr ptr = Marshal.AllocHGlobal(bytes);
            this.Add(ptr);
            return ptr;
        }
        
        public virtual IntPtr
        Realloc(IntPtr oldptr, int bytes)
        {
            IntPtr newptr = Marshal.ReAllocHGlobal(oldptr, (IntPtr)bytes);    
            this.SetRemove(oldptr);        
            this.Add(newptr);
            return newptr;
        }
        
        public virtual void 
        Free(IntPtr ptr)
        {
            this.SetRemove(ptr);
            Marshal.FreeHGlobal(ptr);
        }
        
        public virtual void 
        FreeAll()
        {
            object[] elements = this.ElementsArray;
            foreach (object ptr in elements)
            {
                try
                {
                    this.Free((IntPtr)ptr);
                }
                catch (COMException)
                {
                    Console.WriteLine("Couldn't free; ignoring");
                }
            }
        }
    }

    public enum UnmanagedDataMarker
    {
        PyStringObject,
        PyTupleObject,
        PyListObject,
        None,
    }

    public class BadRefCountException : Exception
    {
        public BadRefCountException(string message): base(message)
        {
        }
    }


    public partial class Python25Mapper : PythonMapper
    {
        private ScriptEngine engine;
        private IAllocator allocator;
        
        private PythonModule dispatcherModule;
        private object dispatcherClass;

        private InterestingPtrMap map = new InterestingPtrMap();
        
        private List<IntPtr> tempObjects = new List<IntPtr>();
        private object _lastException = null;

        public Python25Mapper() : this(ScriptRuntime.Create().GetEngine("py"), new HGlobalAllocator())
        {
        }

        public Python25Mapper(IAllocator alloc) : this(ScriptRuntime.Create().GetEngine("py"), alloc)
        {
        }

        public Python25Mapper(ScriptEngine inEngine, IAllocator alloc)
        {
            this.engine = inEngine;
            this.allocator = alloc;
            this.CreateDispatcher();
        }
        
        public ScriptEngine
        Engine
        {
            get
            {
                return this.engine;
            }
        }
        
        public IntPtr 
        Store(object obj)
        {
            if (obj != null && obj.GetType() == typeof(UnmanagedDataMarker))
            {
                throw new ArgumentTypeException("UnmanagedDataMarkers should not be stored by clients.");
            }
            if (obj == null)
            {
                this.IncRef(this._Py_NoneStruct);
                return this._Py_NoneStruct;
            }
            if (this.map.HasObj(obj))
            {
                IntPtr ptr = this.map.GetPtr(obj);
                this.IncRef(ptr);
                return ptr;
            }
            return this.StoreDispatch(obj);
        }
        
        
        private IntPtr
        StoreObject(object obj)
        {
            IntPtr ptr = this.allocator.Alloc(Marshal.SizeOf(typeof(PyObject)));
            CPyMarshal.WriteIntField(ptr, typeof(PyObject), "ob_refcnt", 1);
            CPyMarshal.WritePtrField(ptr, typeof(PyObject), "ob_type", this.PyBaseObject_Type);
            this.map.Associate(ptr, obj);
            return ptr;
        }
        
        
        public void
        StoreUnmanagedInstance(IntPtr ptr, object obj)
        {
            this.map.WeakAssociate(ptr, obj);
        }
        
        
        public object 
        Retrieve(IntPtr ptr)
        {
            object possibleMarker = this.map.GetObj(ptr);
            if (possibleMarker.GetType() == typeof(UnmanagedDataMarker))
            {
                UnmanagedDataMarker marker = (UnmanagedDataMarker)possibleMarker;
                switch (marker)
                {
                    case UnmanagedDataMarker.None:
                        return null;

                    case UnmanagedDataMarker.PyStringObject:
                        this.ActualiseString(ptr);
                        break;

                    case UnmanagedDataMarker.PyTupleObject:
                        this.ActualiseTuple(ptr);
                        break;

                    case UnmanagedDataMarker.PyListObject:
                        ActualiseList(ptr);
                        break;

                    default:
                        throw new Exception("Found impossible data in pointer map");
                }
            }
            return this.map.GetObj(ptr);
        }
        
        public int 
        RefCount(IntPtr ptr)
        {
            if (this.map.HasPtr(ptr))
            {
                return CPyMarshal.ReadIntField(ptr, typeof(PyObject), "ob_refcnt");
            }
            else
            {
                throw new KeyNotFoundException(String.Format(
                    "RefCount: missing key in pointer map: {0}", ptr));
            }
        }
        
        public void 
        IncRef(IntPtr ptr)
        {
            if (this.map.HasPtr(ptr))
            {
                int count = CPyMarshal.ReadIntField(ptr, typeof(PyObject), "ob_refcnt");
                CPyMarshal.WriteIntField(ptr, typeof(PyObject), "ob_refcnt", count + 1);
            }
            else
            {
                throw new KeyNotFoundException(String.Format(
                    "IncRef: missing key in pointer map: {0}", ptr));
            }
        }
        
        public void 
        DecRef(IntPtr ptr)
        {
            if (this.map.HasPtr(ptr))
            {
                int count = CPyMarshal.ReadIntField(ptr, typeof(PyObject), "ob_refcnt");
                if (count == 0)
                {
                    throw new BadRefCountException("Trying to DecRef an object with ref count 0");
                }
                
                if (count == 1)
                {
                    IntPtr typePtr = CPyMarshal.ReadPtrField(ptr, typeof(PyObject), "ob_type");

                    if (typePtr != IntPtr.Zero)
                    {
                        IntPtr deallocFP = CPyMarshal.ReadPtrField(typePtr, typeof(PyTypeObject), "tp_dealloc");
                        if (deallocFP != IntPtr.Zero)
                        {
                            CPython_destructor_Delegate deallocDgt = (CPython_destructor_Delegate)Marshal.GetDelegateForFunctionPointer(
                                deallocFP, typeof(CPython_destructor_Delegate));
                            deallocDgt(ptr);
                            return;
                        }
                    }
                    this.PyObject_Free(ptr);
                }
                else
                {
                    CPyMarshal.WriteIntField(ptr, typeof(PyObject), "ob_refcnt", count - 1);
                }
            }
            else
            {
                throw new KeyNotFoundException(String.Format(
                    "DecRef: missing key in pointer map: {0}", ptr));
            }
        }
        
        public override void 
        PyObject_Free(IntPtr ptr)
        {
            this.map.Release(ptr);
            this.allocator.Free(ptr);
        }

        public void RememberTempObject(IntPtr ptr)
        {
            this.tempObjects.Add(ptr);
        }

        public void FreeTemps()
        {
            foreach (IntPtr ptr in this.tempObjects)
            {
                this.DecRef(ptr);
            }
            this.tempObjects.Clear();
        }
        
        public object LastException
        {
            get
            {
                return this._lastException;
            }
            set
            {
                this._lastException = value;
            }
        }
        
        public override void
        PyErr_SetString(IntPtr excTypePtr, string message)
        {
            if (excTypePtr == IntPtr.Zero)
            {
                this._lastException = new Exception(message);
            }
            else
            {
                object excType = this.Retrieve(excTypePtr);
                this._lastException = PythonCalls.Call(excType, new object[1]{ message });
            }
        }
        
        
        public IntPtr 
        GetMethodFP(string name)
        {
            Delegate result;
            if (this.dgtMap.TryGetValue(name, out result))
            {
                return Marshal.GetFunctionPointerForDelegate(result);
            }

            switch (name)
            {
                case "PyBaseObject_Dealloc":
                    this.dgtMap[name] = new CPython_destructor_Delegate(this.PyBaseObject_Dealloc);
                    break;
                case "PyTuple_Dealloc":
                    this.dgtMap[name] = new CPython_destructor_Delegate(this.PyTuple_Dealloc);
                    break;
                case "PyList_Dealloc":
                    this.dgtMap[name] = new CPython_destructor_Delegate(this.PyList_Dealloc);
                    break;
                
                default:
                    break;
            }
            return Marshal.GetFunctionPointerForDelegate(this.dgtMap[name]);
        }
        
        
        public override int
        PyCallable_Check(IntPtr objPtr)
        {
            if (Builtin.callable(this.Retrieve(objPtr)))
            {
                return 1;
            }
            return 0;
        }
        
        
        public override void
        Fill__Py_NoneStruct(IntPtr address)
        {
            PyObject none = new PyObject();
            none.ob_refcnt = 1;
            none.ob_type = IntPtr.Zero;
            Marshal.StructureToPtr(none, address, false);
            this.map.Associate(address, UnmanagedDataMarker.None);
        }
        
    }

}
