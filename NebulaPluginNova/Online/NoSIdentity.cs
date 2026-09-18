using AmongUs.Data;
using AmongUs.Matchmaking;
using InnerNet;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using UnityEngine.Rendering;
using Virial.Text;

namespace Nebula.Online;

internal static class PublicRoomBrowser
{
    internal const float RefetchIntervalSeconds = 10f;

    internal const string ClientVersionForServer = "NoS";
}

internal sealed class NoSIdentity
{
    private const string SignaturePrefix = "NOSv1";

    private const string AuthSignaturePrefix = "NOSAUTHv1";

    private static readonly DataSaver Saver = new("NoSIdentity");

    private static readonly StringDataEntry StoredUid = new("uid", Saver, "");

    private static readonly StringDataEntry StoredPrivateKey = new("privateKey", Saver, "");

    private readonly ECDsa ecdsa;

    public string PublicKeyBase64 { get; }

    public string UId { get; private set; } = "";

    public bool IsRegistered => !string.IsNullOrEmpty(UId);

    private NoSIdentity(ECDsa ecdsa)
    {
        this.ecdsa = ecdsa;
        PublicKeyBase64 = Convert.ToBase64String(ecdsa.ExportSubjectPublicKeyInfo());
    }

    private static NoSIdentity? cached = null;

    public static NoSIdentity? Instance
    {
        get
        {
            if (cached != null) return cached;

            try
            {
                if (!string.IsNullOrEmpty(StoredPrivateKey.Value))
                {
                    var ecdsa = ECDsa.Create();
                    ecdsa.ImportPkcs8PrivateKey(Convert.FromBase64String(StoredPrivateKey.Value.Trim()), out _);

                    cached = new NoSIdentity(ecdsa) { UId = StoredUid.Value };
                    return cached;
                }
            }
            catch (Exception e)
            {
                LogUtils.WriteToConsole("NoS: failed to load the stored identity, creating a new one. " + e.Message);
            }

            cached = new NoSIdentity(ECDsa.Create(ECCurve.NamedCurves.nistP256));
            cached.Save();
            return cached;
        }
    }

    public void SetUid(string uid)
    {
        UId = uid ?? "";
        Save();
    }

    private void Save()
    {
        try
        {
            StoredPrivateKey.Value = Convert.ToBase64String(ecdsa.ExportPkcs8PrivateKey());
            StoredUid.Value = UId;
        }
        catch (Exception e)
        {
            LogUtils.WriteToConsole("NoS: failed to save the identity. " + e.Message);
        }
    }

    public Dictionary<string, string> BuildHeaders(string method, byte[] body)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        var bodyDigest = Convert.ToBase64String(SHA256.HashData(body));
        var principal = IsRegistered ? UId : PublicKeyBase64;

        var signingString = string.Join('\n', SignaturePrefix, method, principal, timestamp, nonce, bodyDigest);
        var signature = ecdsa.SignData(
            Encoding.UTF8.GetBytes(signingString),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.Rfc3279DerSequence);

        var headers = new Dictionary<string, string>
        {
            ["X-NoS-Timestamp"] = timestamp,
            ["X-NoS-Nonce"] = nonce,
            ["X-NoS-Signature"] = Convert.ToBase64String(signature),
        };
        if (IsRegistered) headers["X-NoS-Uid"] = UId;
        else headers["X-NoS-Public-Key"] = PublicKeyBase64;

        return headers;
    }

    private static string BuildAuthSigningString(string uid, string gameCode, string issuedAt, byte[] challenge)
    {
        var challengeDigest = Convert.ToBase64String(SHA256.HashData(challenge));
        return string.Join('\n', AuthSignaturePrefix, uid, gameCode, issuedAt, challengeDigest);
    }

    public (string Signature, string IssuedAt)? CreateAuthProof(string gameCode, byte[] challenge)
    {
        if (!IsRegistered) return null;

        var issuedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var signingString = BuildAuthSigningString(UId, gameCode, issuedAt, challenge);
        var signature = ecdsa.SignData(
            Encoding.UTF8.GetBytes(signingString),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.Rfc3279DerSequence);
        return (Convert.ToBase64String(signature), issuedAt);
    }
}
