//! xoshiro256** for the solver's rollouts and expansion coin flips. C# draws from `System.Random`,
//! whose sequence the engine's `DotNetRandom` reproduces for the simulator, but a search cannot
//! replay across languages anyway (wall-clock iteration counts), so the solver takes the faster
//! generator and keeps only the guarantee that matters: the same seed gives the same result.

#[derive(Clone, Debug)]
pub struct Xoshiro256 {
    s: [u64; 4],
}

impl Xoshiro256 {
    /// Expands the C# `int` seed through splitmix64, the seeding the xoshiro authors recommend.
    pub fn from_seed(seed: i32) -> Xoshiro256 {
        let mut x = seed as i64 as u64;
        let mut s = [0u64; 4];
        for word in &mut s {
            x = x.wrapping_add(0x9E37_79B9_7F4A_7C15);
            let mut z = x;
            z = (z ^ (z >> 30)).wrapping_mul(0xBF58_476D_1CE4_E5B9);
            z = (z ^ (z >> 27)).wrapping_mul(0x94D0_49BB_1331_11EB);
            *word = z ^ (z >> 31);
        }
        Xoshiro256 { s }
    }

    pub fn next_u64(&mut self) -> u64 {
        let result = self.s[1].wrapping_mul(5).rotate_left(7).wrapping_mul(9);
        let t = self.s[1] << 17;
        self.s[2] ^= self.s[0];
        self.s[3] ^= self.s[1];
        self.s[1] ^= self.s[2];
        self.s[0] ^= self.s[3];
        self.s[2] ^= t;
        self.s[3] = self.s[3].rotate_left(45);
        result
    }

    /// Uniform in `[0, 1)` with 53 random bits, the same range as `Random.NextDouble()`.
    pub fn next_f64(&mut self) -> f64 {
        (self.next_u64() >> 11) as f64 * (1.0 / (1u64 << 53) as f64)
    }

    /// Uniform in `[0, n)`; multiply-high on the top 32 bits, so no modulo bias and no division.
    pub fn next_below(&mut self, n: u32) -> u32 {
        (((self.next_u64() >> 32) * n as u64) >> 32) as u32
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn same_seed_same_stream() {
        let mut a = Xoshiro256::from_seed(42);
        let mut b = Xoshiro256::from_seed(42);
        for _ in 0..100 {
            assert_eq!(a.next_u64(), b.next_u64());
        }
    }

    #[test]
    fn different_seeds_differ_and_negative_seeds_are_distinct() {
        let mut a = Xoshiro256::from_seed(1);
        let mut b = Xoshiro256::from_seed(2);
        let mut c = Xoshiro256::from_seed(-1);
        let (x, y, z) = (a.next_u64(), b.next_u64(), c.next_u64());
        assert_ne!(x, y);
        assert_ne!(x, z);
    }

    #[test]
    fn samples_stay_in_range() {
        let mut rng = Xoshiro256::from_seed(7);
        for _ in 0..10_000 {
            let f = rng.next_f64();
            assert!((0.0..1.0).contains(&f));
            assert!(rng.next_below(36) < 36);
            assert_eq!(rng.next_below(1), 0);
        }
    }
}
