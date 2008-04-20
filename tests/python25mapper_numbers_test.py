
import unittest
from tests.utils.runtest import makesuite, run

from tests.utils.memory import CreateTypes

from System import Int64, IntPtr
from Ironclad import CPyMarshal, Python25Mapper
from Ironclad.Structs import PyObject
from IronPython.Hosting import PythonEngine


class Python25Mapper_PyInt_Test(unittest.TestCase):

    def testStoreInt(self):
        engine = PythonEngine()
        mapper = Python25Mapper(engine)
        deallocTypes = CreateTypes(mapper)
        
        try:
            for value in (0, 2147483647, -2147483648):
                ptr = mapper.Store(value)
                self.assertEquals(mapper.Retrieve(ptr), value, "stored/retrieved wrong")
                self.assertEquals(CPyMarshal.ReadPtrField(ptr, PyObject, "ob_type"), mapper.PyInt_Type, "bad type")
                mapper.DecRef(ptr)
        finally:
            deallocTypes()
    
    
    def testPyInt_FromLong(self):
        engine = PythonEngine()
        mapper = Python25Mapper(engine)
        deallocTypes = CreateTypes(mapper)
        
        try:
            for value in (0, 2147483647, -2147483648):
                ptr = mapper.PyInt_FromLong(value)
                self.assertEquals(mapper.Retrieve(ptr), value, "stored/retrieved wrong")
                self.assertEquals(CPyMarshal.ReadPtrField(ptr, PyObject, "ob_type"), mapper.PyInt_Type, "bad type")
                mapper.DecRef(ptr)
        finally:
            deallocTypes()
    
    def testPyInt_FromSsize_t(self):
        engine = PythonEngine()
        mapper = Python25Mapper(engine)
        deallocTypes = CreateTypes(mapper)
        
        try:
            for value in (0, 2147483647, -2147483648):
                ptr = mapper.PyInt_FromSsize_t(value)
                self.assertEquals(mapper.Retrieve(ptr), value, "stored/retrieved wrong")
                self.assertEquals(CPyMarshal.ReadPtrField(ptr, PyObject, "ob_type"), mapper.PyInt_Type, "bad type")
                mapper.DecRef(ptr)
        finally:
            deallocTypes()


    def testPyInt_AsLong(self):
        engine = PythonEngine()
        mapper = Python25Mapper(engine)
        deallocTypes = CreateTypes(mapper)
        
        try:
            for value in (0, 2147483647, -2147483648):
                ptr = mapper.PyInt_FromLong(value)
                self.assertEquals(mapper.PyInt_AsLong(ptr), value, "failed to map back")
                self.assertEquals(CPyMarshal.ReadPtrField(ptr, PyObject, "ob_type"), mapper.PyInt_Type, "bad type")
        finally:
            deallocTypes()
        

class Python25Mapper_PyLong_Test(unittest.TestCase):

    def testStoreLong(self):
        engine = PythonEngine()
        mapper = Python25Mapper(engine)
        deallocTypes = CreateTypes(mapper)
        
        try:
            for value in (5555555555, -5555555555, long(0)):
                ptr = mapper.Store(value)
                self.assertEquals(mapper.Retrieve(ptr), value, "stored/retrieved wrong")
                self.assertEquals(CPyMarshal.ReadPtrField(ptr, PyObject, "ob_type"), mapper.PyLong_Type, "bad type")
                mapper.DecRef(ptr)
        finally:
            deallocTypes()
    
    def testPyLong_FromLongLong(self):
        engine = PythonEngine()
        mapper = Python25Mapper(engine)
        deallocTypes = CreateTypes(mapper)
        
        try:
            for value in map(Int64, (5555555555, -5555555555, 0)):
                ptr = mapper.PyLong_FromLongLong(value)
                self.assertEquals(mapper.Retrieve(ptr), value, "stored/retrieved wrong")
                self.assertEquals(CPyMarshal.ReadPtrField(ptr, PyObject, "ob_type"), mapper.PyLong_Type, "bad type")
        finally:
            deallocTypes()


class Python25Mapper_PyFloat_FromDouble_Test(unittest.TestCase):

    def testStoreFloat(self):
        engine = PythonEngine()
        mapper = Python25Mapper(engine)
        deallocTypes = CreateTypes(mapper)
        
        try:
            for value in (0.0, 3.3e33, -3.3e-33):
                ptr = mapper.Store(value)
                self.assertEquals(mapper.Retrieve(ptr), value, "stored/retrieved wrong")
                self.assertEquals(CPyMarshal.ReadPtrField(ptr, PyObject, "ob_type"), mapper.PyFloat_Type, "bad type")
                mapper.DecRef(ptr)
        finally:
            deallocTypes()
    
    def testPyFloat_FromDouble(self):
        engine = PythonEngine()
        mapper = Python25Mapper(engine)
        deallocTypes = CreateTypes(mapper)
        
        try:
            for value in (0.0, 3.3e33, -3.3e-33):
                ptr = mapper.PyFloat_FromDouble(value)
                self.assertEquals(mapper.Retrieve(ptr), value, "stored/retrieved wrong")
                self.assertEquals(CPyMarshal.ReadPtrField(ptr, PyObject, "ob_type"), mapper.PyFloat_Type, "bad type")
                mapper.DecRef(ptr)
        finally:
            deallocTypes()



suite = makesuite(
    Python25Mapper_PyInt_Test,
    Python25Mapper_PyLong_Test,
    Python25Mapper_PyFloat_FromDouble_Test,
)

if __name__ == '__main__':
    run(suite)