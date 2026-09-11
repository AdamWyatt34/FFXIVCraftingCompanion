//! Seeded `System.Random` as .NET implements it for a caller-supplied seed: Knuth's subtractive
//! generator, kept by .NET for sequence compatibility. Arithmetic is wrapping because the C#
//! source runs unchecked and large seeds do overflow during seeding.

const MBIG: i32 = i32::MAX;
const MSEED: i32 = 161_803_398;

#[derive(Clone, Debug)]
pub struct DotNetRandom {
    seed_array: [i32; 56],
    inext: usize,
    inextp: usize,
}

impl DotNetRandom {
    pub fn new(seed: i32) -> DotNetRandom {
        let mut seed_array = [0i32; 56];
        let subtraction = if seed == i32::MIN {
            i32::MAX
        } else {
            seed.wrapping_abs()
        };
        let mut mj = MSEED.wrapping_sub(subtraction);
        seed_array[55] = mj;
        let mut mk = 1i32;
        let mut ii = 0usize;
        for _ in 1..55 {
            ii += 21;
            if ii >= 55 {
                ii -= 55;
            }
            seed_array[ii] = mk;
            mk = mj.wrapping_sub(mk);
            if mk < 0 {
                mk = mk.wrapping_add(MBIG);
            }
            mj = seed_array[ii];
        }
        for _ in 1..5 {
            for i in 1..56 {
                let mut n = i + 30;
                if n >= 55 {
                    n -= 55;
                }
                seed_array[i] = seed_array[i].wrapping_sub(seed_array[1 + n]);
                if seed_array[i] < 0 {
                    seed_array[i] = seed_array[i].wrapping_add(MBIG);
                }
            }
        }
        DotNetRandom {
            seed_array,
            inext: 0,
            inextp: 21,
        }
    }

    fn internal_sample(&mut self) -> i32 {
        let mut inext = self.inext + 1;
        if inext >= 56 {
            inext = 1;
        }
        let mut inextp = self.inextp + 1;
        if inextp >= 56 {
            inextp = 1;
        }
        let mut ret = self.seed_array[inext].wrapping_sub(self.seed_array[inextp]);
        if ret == MBIG {
            ret -= 1;
        }
        if ret < 0 {
            ret = ret.wrapping_add(MBIG);
        }
        self.seed_array[inext] = ret;
        self.inext = inext;
        self.inextp = inextp;
        ret
    }

    /// `Random.NextDouble()`: a sample in `[0, 1)`.
    pub fn next_double(&mut self) -> f64 {
        self.internal_sample() as f64 * (1.0 / MBIG as f64)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn seed_42_matches_dotnet() {
        let mut rng = DotNetRandom::new(42);
        assert_eq!(rng.next_double(), 0.6681064659115423);
        assert_eq!(rng.next_double(), 0.14090729837348093);
    }

    #[test]
    fn negative_seed_uses_magnitude() {
        let mut negative = DotNetRandom::new(-7);
        let mut positive = DotNetRandom::new(7);
        assert_eq!(negative.next_double(), positive.next_double());
    }
}
