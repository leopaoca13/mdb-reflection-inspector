using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace MyMod
{
    // Provides a simple way to convert a UnityEngine.Vector3 to an IntPtr
    // backed by a single reusable unmanaged buffer to avoid allocating every frame.
    public static class Vector3Extensions
    {
        private static IntPtr _vec3Buffer = IntPtr.Zero;

        // Convert a Vector3 to an IntPtr pointing to an unmanaged copy of the struct.
        // The returned pointer is valid until the process exits or until FreeBuffer() is called.
        public static IntPtr ToIntPtr(this Vector3 v)
        {
            if (_vec3Buffer == IntPtr.Zero)
            {
                _vec3Buffer = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(Vector3)));
            }

            Marshal.StructureToPtr(v, _vec3Buffer, false);
            return _vec3Buffer;
        }

        // Call this from shutdown/unload if you want to free the unmanaged buffer.
        public static void FreeBuffer()
        {
            if (_vec3Buffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_vec3Buffer);
                _vec3Buffer = IntPtr.Zero;
            }
        }
    }
}
