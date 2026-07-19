using System.Security.Cryptography.X509Certificates;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto.Prng;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;

namespace WebSockets.FakeServer;

/// <summary>
/// Generates ephemeral, in-memory self-signed certificates for tests that
/// need to exercise TLS/certificate-validation behavior. Most tests just need
/// "some untrusted cert" and don't care about its specific identity; those
/// should use <see cref="Shared"/> rather than <see cref="Create"/>, since
/// generating and importing a fresh certificate per test is both slower
/// (RSA-2048 generation isn't free) and was the actual source of real
/// intermittent failures under xUnit's default parallel test execution, see
/// the notes on <see cref="Create"/> for that history.
/// </summary>
public static class SelfSignedCertificateFactory
{
    private static readonly object ImportLock = new object();
    private static readonly Lazy<X509Certificate2> Cached = new(() => Create());

    /// <summary>
    /// A single self-signed certificate, generated once and reused for the
    /// lifetime of the process. Callers must not dispose this instance, it's
    /// owned by this cache. Reusing one certificate as a TLS server identity
    /// across many connections is normal, this is exactly what real server
    /// code (e.g. Kestrel) does too, not a shortcut specific to tests.
    /// </summary>
    public static X509Certificate2 Shared => Cached.Value;

    /// <summary>
    /// Generates a fresh, distinct self-signed certificate with a 2048-bit
    /// RSA key pair, valid from 5 minutes ago to 5 minutes from now. Prefer
    /// <see cref="Shared"/> unless a test specifically needs a certificate
    /// guaranteed distinct from others; callers of this method own the
    /// returned instance and are responsible for disposing it.
    ///
    /// Importing a PFX writes a temporary private-key file into a shared,
    /// LSA-protected, profile-based key container, then deletes it on
    /// disposal. xUnit runs different test classes in parallel by default,
    /// and several tests used to import a certificate each; concurrent
    /// writes/deletes against that shared store were a real, confirmed
    /// source of sporadic "Local Security Authority cannot be contacted"
    /// errors under contention. ImportLock serializes just the import step
    /// (not certificate generation itself) to guard against that, uniformly
    /// on both platforms, though <see cref="Shared"/> existing at all means
    /// this path, and therefore that contention, should now be rare.
    ///
    /// X509KeyStorageFlags.EphemeralKeySet, which avoids the on-disk store
    /// entirely, was tried as a per-platform alternative here (it's only
    /// available on .NET Framework 4.7.2+/net5.0+, so net462 would still have
    /// needed this lock regardless), but produced intermittent TLS handshake
    /// failures when the resulting certificate was used as a server identity
    /// via SslStream.AuthenticateAsServerAsync, root cause not confirmed.
    /// Reverted in favor of this simpler, uniformly-correct approach.
    ///
    /// Uses BouncyCastle rather than System.Security.Cryptography.X509Certificates.CertificateRequest
    /// because CertificateRequest isn't available on .NET Framework until 4.7.2
    /// either. BouncyCastle.Cryptography supports both net461+ and net5.0+ directly.
    /// </summary>
    /// <param name="subjectName">The certificate's subject/issuer distinguished name. Defaults to "CN=localhost".</param>
    public static X509Certificate2 Create(string subjectName = "CN=localhost")
    {
        var random = new SecureRandom(new CryptoApiRandomGenerator());

        var keyPairGenerator = new RsaKeyPairGenerator();
        keyPairGenerator.Init(new KeyGenerationParameters(random, 2048));
        var keyPair = keyPairGenerator.GenerateKeyPair();

        var generator = new X509V3CertificateGenerator();
        generator.SetSerialNumber(BigInteger.ValueOf(DateTime.UtcNow.Ticks));

        var subject = new X509Name(subjectName);
        generator.SetSubjectDN(subject);
        generator.SetIssuerDN(subject);
        generator.SetNotBefore(DateTime.UtcNow.AddMinutes(-5));
        generator.SetNotAfter(DateTime.UtcNow.AddMinutes(5));
        generator.SetPublicKey(keyPair.Public);

        var signatureFactory = new Asn1SignatureFactory("SHA256WITHRSA", keyPair.Private, random);
        var certificate = generator.Generate(signatureFactory);

        var store = new Pkcs12StoreBuilder().Build();
        var certificateEntry = new X509CertificateEntry(certificate);
        store.SetCertificateEntry(subjectName, certificateEntry);
        store.SetKeyEntry(subjectName, new AsymmetricKeyEntry(keyPair.Private), new[] { certificateEntry });

        using var pfxStream = new MemoryStream();
        store.Save(pfxStream, Array.Empty<char>(), random);
        var pfxBytes = pfxStream.ToArray();

        lock (ImportLock)
        {
            return new X509Certificate2(pfxBytes, string.Empty, X509KeyStorageFlags.Exportable);
        }
    }
}