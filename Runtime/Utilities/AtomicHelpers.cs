using Unity.Mathematics;

namespace Scellecs.Morpeh.Graphics.Utilities
{
    internal struct AtomicHelpers
    {
        public const uint kNumBitsInLong = sizeof(long) * 8;

        public static void IndexToQwIndexAndMask(int index, out int qwIndex, out long mask)
        {
            uint i = (uint)index;
            uint qw = i / kNumBitsInLong;
            uint shift = i % kNumBitsInLong;

            qwIndex = (int)qw;
            mask = 1L << (int)shift;
        }

        // This function doesn't actually return the value as using it will make the generated code less optimal
        public static unsafe void AtomicAnd(long* qwords, int index, long value)
        {
#if UNITY_BURST_EXPERIMENTAL_ATOMIC_INTRINSICS
            Unity.Burst.Intrinsics.Common.InterlockedAnd(ref qwords[index], value);
#else
            // TODO: Replace this with atomic AND once it is available
            long currentValue = System.Threading.Interlocked.Read(ref qwords[index]);
            for (; ; )
            {
                // If the AND wouldn't change any bits, no need to issue the atomic
                if ((currentValue & value) == currentValue)
                    return;

                long newValue = currentValue & value;
                long prevValue =
                    System.Threading.Interlocked.CompareExchange(ref qwords[index], newValue, currentValue);

                // If the value was equal to the expected value, we know that our atomic went through
                if (prevValue == currentValue)
                    return;

                currentValue = prevValue;
            }
#endif
        }

        // This function doesn't actually return the value as using it will make the generated code less optimal
        public static unsafe void AtomicOr(long* qwords, int index, long value)
        {
#if UNITY_BURST_EXPERIMENTAL_ATOMIC_INTRINSICS
            Unity.Burst.Intrinsics.Common.InterlockedOr(ref qwords[index], value);
#else
            // TODO: Replace this with atomic OR once it is available
            long currentValue = System.Threading.Interlocked.Read(ref qwords[index]);
            for (; ; )
            {
                // If the OR wouldn't change any bits, no need to issue the atomic
                if ((currentValue | value) == currentValue)
                    return;

                long newValue = currentValue | value;
                long prevValue =
                    System.Threading.Interlocked.CompareExchange(ref qwords[index], newValue, currentValue);

                // If the value was equal to the expected value, we know that our atomic went through
                if (prevValue == currentValue)
                    return;

                currentValue = prevValue;
            }
#endif
        }

        public static unsafe float AtomicMin(float* floats, int index, float value)
        {
            float currentValue = floats[index];

            // Never propagate NaNs to memory
            if (float.IsNaN(value))
                return currentValue;

            int* floatsAsInts = (int*)floats;
            int valueAsInt = math.asint(value);

            // Do the CAS operations as ints to avoid problems with NaNs
            for (; ; )
            {
                // If currentValue is NaN, this comparison will fail
                if (currentValue <= value)
                    return currentValue;

                int currentValueAsInt = math.asint(currentValue);

                int newValue = valueAsInt;
                int prevValue = System.Threading.Interlocked.CompareExchange(ref floatsAsInts[index], newValue, currentValueAsInt);
                float prevValueAsFloat = math.asfloat(prevValue);

                // If the value was equal to the expected value, we know that our atomic went through
                // NOTE: This comparison MUST be an integer comparison, as otherwise NaNs
                // would result in an infinite loop
                if (prevValue == currentValueAsInt)
                    return prevValueAsFloat;

                currentValue = prevValueAsFloat;
            }
        }

        public static unsafe float AtomicMax(float* floats, int index, float value)
        {
            float currentValue = floats[index];

            // Never propagate NaNs to memory
            if (float.IsNaN(value))
                return currentValue;

            int* floatsAsInts = (int*)floats;
            int valueAsInt = math.asint(value);

            // Do the CAS operations as ints to avoid problems with NaNs
            for (; ; )
            {
                // If currentValue is NaN, this comparison will fail
                if (currentValue >= value)
                    return currentValue;

                int currentValueAsInt = math.asint(currentValue);

                int newValue = valueAsInt;
                int prevValue = System.Threading.Interlocked.CompareExchange(ref floatsAsInts[index], newValue, currentValueAsInt);
                float prevValueAsFloat = math.asfloat(prevValue);

                // If the value was equal to the expected value, we know that our atomic went through
                // NOTE: This comparison MUST be an integer comparison, as otherwise NaNs
                // would result in an infinite loop
                if (prevValue == currentValueAsInt)
                    return prevValueAsFloat;

                currentValue = prevValueAsFloat;
            }
        }
    }
}
