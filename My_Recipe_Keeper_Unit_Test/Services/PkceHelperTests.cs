using My_Recipe_Keeper.Core.Services;

namespace My_Recipe_Keeper.Tests.Services
{
    public class PkceHelperTests
    {
        [Fact]
        public void GenerateCodeVerifier_ProducesUrlSafeStringWithNoPadding()
        {
            var verifier = PkceHelper.GenerateCodeVerifier();

            Assert.DoesNotContain('+', verifier);
            Assert.DoesNotContain('/', verifier);
            Assert.DoesNotContain('=', verifier);
        }

        [Fact]
        public void GenerateCodeVerifier_IsRandomAcrossCalls()
        {
            var a = PkceHelper.GenerateCodeVerifier();
            var b = PkceHelper.GenerateCodeVerifier();

            Assert.NotEqual(a, b);
        }

        [Fact]
        public void GenerateCodeChallenge_IsDeterministicForSameVerifier()
        {
            var verifier = "fixed-verifier-value";

            var challengeA = PkceHelper.GenerateCodeChallenge(verifier);
            var challengeB = PkceHelper.GenerateCodeChallenge(verifier);

            Assert.Equal(challengeA, challengeB);
        }

        [Fact]
        public void GenerateCodeChallenge_DiffersFromVerifier()
        {
            var verifier = PkceHelper.GenerateCodeVerifier();

            var challenge = PkceHelper.GenerateCodeChallenge(verifier);

            Assert.NotEqual(verifier, challenge);
        }
    }
}
