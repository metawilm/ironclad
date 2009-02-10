using System;
using System.Runtime.InteropServices;

using IronPython.Runtime;

using Ironclad.Structs;

namespace Ironclad
{
    public partial class Python25Mapper : Python25Api
    {
        public override IntPtr
        PyMethod_New(IntPtr funcPtr, IntPtr selfPtr, IntPtr klassPtr)
        {
            object func = null;
            if (funcPtr != IntPtr.Zero)
            {
                func = this.Retrieve(funcPtr);
            }
            object self = null;
            if (selfPtr != IntPtr.Zero)
            {
                self = this.Retrieve(selfPtr);
            }
            object klass = null;
            if (klassPtr != IntPtr.Zero)
            {
                klass = this.Retrieve(klassPtr);
            }

            return this.Store(new Method(func, self, klass));
        }
        
        private IntPtr
        Store(Method meth)
        {
            uint size = (uint)Marshal.SizeOf(typeof(PyMethodObject));
            IntPtr methPtr = this.allocator.Alloc(size);
            CPyMarshal.Zero(methPtr, size);
            
            CPyMarshal.WriteIntField(methPtr, typeof(PyMethodObject), "ob_refcnt", 1);
            CPyMarshal.WritePtrField(methPtr, typeof(PyMethodObject), "ob_type", this.PyMethod_Type);
            CPyMarshal.WritePtrField(methPtr, typeof(PyMethodObject), "im_func", this.Store(meth.im_func));
            CPyMarshal.WritePtrField(methPtr, typeof(PyMethodObject), "im_self", this.Store(meth.im_self));
            CPyMarshal.WritePtrField(methPtr, typeof(PyMethodObject), "im_class", this.Store(meth.im_class));
            
            this.map.Associate(methPtr, meth);
            return methPtr;
        }
    }
}
