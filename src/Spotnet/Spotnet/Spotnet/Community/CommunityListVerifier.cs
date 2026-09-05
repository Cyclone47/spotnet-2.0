using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using NLog;

namespace Spotnet.Community;

/// <summary>
/// Checks a downloaded moderation list against a detached signature published next to it.
/// </summary>
/// <remarks>
/// The lists arrive over plain HTTP and decide which posters this client trusts, so whoever
/// controls the path controls that decision. A community that signs its lists closes that
/// gap: it publishes "&lt;list&gt;.sig" holding a base64 RSA-SHA256 signature over the list
/// bytes, and clients configure the matching public key.
///
/// This is opt-in on purpose. With no key configured nothing is verified and the lists are
/// used exactly as before, because the community these defaults point at does not sign its
/// lists yet. Rejecting an unsigned list only ever happens when the user asked for it.
/// </remarks>
internal static class CommunityListVerifier
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    /// <summary>The extension appended to a list URL to find its detached signature.</summary>
    internal const string SignatureExtension = ".sig";

    /// <summary>
    /// Decides whether a freshly downloaded list may be put into use.
    /// </summary>
    /// <param name="localPath">The downloaded file, not yet moved into place.</param>
    /// <param name="listUrl">Where it came from; the signature sits beside it.</param>
    internal static bool MayUse(string localPath, string listUrl)
    {
        CommunityModeration moderation = CommunityConfig.Current.Moderation;
        bool haveKey = !string.IsNullOrWhiteSpace(moderation.SignaturePublicKeyXml);

        if (!haveKey)
        {
            // Nothing to check against. Only refuse if the user insisted on signatures,
            // which the configuration validator already treats as a misconfiguration.
            if (moderation.RequireSignedLists)
            {
                Log.Warn("Ondertekende lijsten zijn verplicht gesteld maar er is geen publieke sleutel ingesteld; {0} wordt niet gebruikt.", listUrl);
                return false;
            }

            return true;
        }

        SignatureResult result = Check(localPath, listUrl, moderation.SignaturePublicKeyXml);
        switch (result)
        {
            case SignatureResult.Valid:
                return true;

            case SignatureResult.Missing:
                if (moderation.RequireSignedLists)
                {
                    Log.Warn("Geen handtekening gevonden voor {0}; de lijst wordt niet gebruikt.", listUrl);
                    return false;
                }

                Log.Debug("Geen handtekening gevonden voor {0}; de lijst wordt ongeverifieerd gebruikt.", listUrl);
                return true;

            default:
                // A signature that is present and wrong is always refused: it means either
                // tampering or a key mismatch, and neither should quietly pass.
                Log.Warn("Ongeldige handtekening voor {0}; de lijst wordt niet gebruikt.", listUrl);
                return false;
        }
    }

    private enum SignatureResult
    {
        Valid,
        Invalid,
        Missing
    }

    private static SignatureResult Check(string localPath, string listUrl, string publicKeyXml)
    {
        string signature;
        try
        {
            using WebClient client = new WebClient();
            signature = client.DownloadString(listUrl + SignatureExtension);
        }
        catch (WebException)
        {
            return SignatureResult.Missing;
        }
        catch (Exception ex)
        {
            Log.Debug("Handtekening ophalen voor {0} mislukte: {1}", listUrl, ex.Message);
            return SignatureResult.Missing;
        }

        if (string.IsNullOrWhiteSpace(signature))
        {
            return SignatureResult.Missing;
        }

        try
        {
            byte[] signatureBytes = Convert.FromBase64String(signature.Trim());
            byte[] data = File.ReadAllBytes(localPath);

            using RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
            rsa.FromXmlString(publicKeyXml);
            using SHA256 sha = SHA256.Create();

            return rsa.VerifyData(data, sha, signatureBytes)
                ? SignatureResult.Valid
                : SignatureResult.Invalid;
        }
        catch (Exception ex)
        {
            Log.Debug("Handtekening controleren voor {0} mislukte: {1}", listUrl, ex.Message);
            return SignatureResult.Invalid;
        }
    }
}
