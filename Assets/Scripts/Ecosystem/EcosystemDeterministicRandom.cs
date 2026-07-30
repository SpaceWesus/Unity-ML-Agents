namespace Turtle.Ecosystem
{
    /// <summary>
    /// Persisted-cursor deterministic random source. A save/reload continuation consumes
    /// exactly the same sequence as an uninterrupted simulation.
    /// </summary>
    public sealed class EcosystemDeterministicRandom
    {
        private readonly EcosystemWorldState state;

        public EcosystemDeterministicRandom(EcosystemWorldState worldState)
        {
            state = worldState;
        }

        public float Next01(string salt)
        {
            state.simulationSequence++;
            var value = StableHash(salt);
            value ^= unchecked((uint)state.worldSeed * 0x9E3779B9u);
            value ^= unchecked((uint)state.day * 0x85EBCA6Bu);
            value ^= unchecked((uint)state.simulationSequence * 0xC2B2AE35u);
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return (value & 0x00FFFFFFu) / 16777216f;
        }

        public int Range(int minimumInclusive, int maximumExclusive, string salt)
        {
            if (maximumExclusive <= minimumInclusive)
            {
                return minimumInclusive;
            }

            return minimumInclusive +
                   (int)(Next01(salt) * (maximumExclusive - minimumInclusive));
        }

        public static uint StableHash(string value)
        {
            unchecked
            {
                var hash = 2166136261u;
                if (value == null)
                {
                    return hash;
                }

                for (var index = 0; index < value.Length; index++)
                {
                    hash ^= value[index];
                    hash *= 16777619u;
                }

                return hash;
            }
        }
    }
}
