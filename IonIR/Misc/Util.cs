using System;
using System.Collections.Generic;
using System.Linq;
using Ion.Engine.Tracking;
using Ion.IR.Constructs;

namespace Ion.IR.Target
{
    public static class Util
    {
        public static bool IsPointerNull(IntPtr pointer)
        {
            return pointer == IntPtr.Zero;
        }

        // TODO: Needs unit testing. Might not be able to cast TWrapper (because of constructor params).
        public static TWrapper[] Wrap<TWrapper, TValue>(this TValue[] values) where TWrapper : LlvmWrapper<TValue>
        {
            // Create the buffer list.
            List<TWrapper> buffer = new List<TWrapper>();

            // Loop through all values.
            foreach (TValue value in values)
            {
                // Wrap and append value to the buffer list.
                buffer.Add((TWrapper)new LlvmWrapper<TValue>(value));
            }

            // Return the buffer list as an array.
            return buffer.ToArray();
        }

        public static Dictionary<string, T> ToDic<T>(this T[] values) where T : INamed
        {
            return values.ToDictionary<string, T>((INamed value) =>
            {
                return value.Name;
            });
        }

        public static T[] Unwrap<T>(this IWrapper<T>[] values)
        {
            // Create the buffer list.
            List<T> buffer = new List<T>();

            // Loop through all values.
            foreach (LlvmWrapper<T> value in values)
            {
                // Unwrap and append value to the buffer list.
                buffer.Add(value.Unwrap());
            }

            // Return the buffer list as an array.
            return buffer.ToArray();
        }

        public static LlvmValue[] AsLlvmValues(this Value[] values)
        {
            // Create the buffer list.
            List<LlvmValue> buffer = new List<LlvmValue>();

            // Loop through all the values.
            foreach (Value value in values)
            {
                // Append value to the buffer.
                buffer.Add(value.AsLlvmValue());
            }

            // Return the buffer as an array.
            return buffer.ToArray();
        }
    }
}