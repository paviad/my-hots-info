using System;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Security;

namespace CASCExplorer
{
    [SuppressUnmanagedCodeSecurity]
    internal class UnsafeNativeMethods
    {
        // This doesn't work in .Net Core, the entry point doesn't exist:
        // [DllImport("kernel32.dll", EntryPoint = "CopyMemory", SetLastError = false)]
        // This works instead, with a different entry point:
        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory", SetLastError = false)]
        [SecurityCritical]
        internal static extern void CopyMemory(IntPtr dest, IntPtr src, uint count);

        [DllImport("kernel32.dll", EntryPoint = "CopyMemory", SetLastError = false)]
        [SecurityCritical]
        internal static extern unsafe void CopyMemoryPtr(void* dest, void* src, uint count);
    }

    public static class FastStruct<T> where T : struct
    {
        private static readonly GetPtrDelegate GetPtr = BuildGetPtrMethod();
        private static readonly PtrToStructureDelegateIntPtr PtrToStructureIntPtr = BuildLoadFromIntPtrMethod();
        private static readonly PtrToStructureDelegateBytePtr PtrToStructureBytePtr = BuildLoadFromBytePtrMethod();

        public static readonly int Size = Marshal.SizeOf(typeof(T));

        private static DynamicMethod methodGetPtr;
        private static DynamicMethod methodLoadIntPtr;
        private static DynamicMethod methodLoadBytePtr;

        public static T PtrToStructure(IntPtr ptr)
        {
            return PtrToStructureIntPtr(ptr);
        }

        public static unsafe T PtrToStructure(byte* ptr)
        {
            return PtrToStructureBytePtr(ptr);
        }

        public static T[] ReadArray(IntPtr source, int bytesCount)
        {
            var elementSize = (uint)Size;

            var buffer = new T[bytesCount / elementSize];

            if (bytesCount > 0)
            {
                var p = GetPtr(ref buffer[0]);
                UnsafeNativeMethods.CopyMemory(p, source, (uint)bytesCount);
            }

            return buffer;
        }

        private static GetPtrDelegate BuildGetPtrMethod()
        {
            methodGetPtr = new DynamicMethod(
                "GetStructPtr<" + typeof(T).FullName + ">",
                typeof(IntPtr),
                new[] { typeof(T).MakeByRefType() },
                typeof(FastStruct<T>).Module);

            var generator = methodGetPtr.GetILGenerator();
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Conv_U);
            generator.Emit(OpCodes.Ret);
            return (GetPtrDelegate)methodGetPtr.CreateDelegate(typeof(GetPtrDelegate));
        }

        private static PtrToStructureDelegateBytePtr BuildLoadFromBytePtrMethod()
        {
            methodLoadBytePtr = new DynamicMethod(
                "PtrToStructureBytePtr<" + typeof(T).FullName + ">",
                typeof(T),
                new[] { typeof(byte*) },
                typeof(FastStruct<T>).Module);

            var generator = methodLoadBytePtr.GetILGenerator();
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldobj, typeof(T));
            generator.Emit(OpCodes.Ret);

            return (PtrToStructureDelegateBytePtr)methodLoadBytePtr.CreateDelegate(
                typeof(PtrToStructureDelegateBytePtr));
        }

        private static PtrToStructureDelegateIntPtr BuildLoadFromIntPtrMethod()
        {
            methodLoadIntPtr = new DynamicMethod(
                "PtrToStructureIntPtr<" + typeof(T).FullName + ">",
                typeof(T),
                new[] { typeof(IntPtr) },
                typeof(FastStruct<T>).Module);

            var generator = methodLoadIntPtr.GetILGenerator();
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldobj, typeof(T));
            generator.Emit(OpCodes.Ret);

            return (PtrToStructureDelegateIntPtr)methodLoadIntPtr.CreateDelegate(typeof(PtrToStructureDelegateIntPtr));
        }

        private delegate IntPtr GetPtrDelegate(ref T value);

        private delegate T PtrToStructureDelegateIntPtr(IntPtr pointer);

        private unsafe delegate T PtrToStructureDelegateBytePtr(byte* pointer);
    }
}
