using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Island.Core.StreamBox
{
    /// <summary>Where a pairing attempt has got to.</summary>
    public enum PairingStatus
    {
        /// <summary>Nobody has answered on the phone yet. Keep polling.</summary>
        Pending,
        /// <summary>The person said no. Stop, and say so.</summary>
        Denied,
        /// <summary>The code ran out, or the token was already collected. Start again.</summary>
        Expired,
        /// <summary>Approved, and the device token is in hand. It is handed over exactly once.</summary>
        Approved,
    }

    /// <summary>The answer to <c>POST /pair/start</c>: what to show, and how to wait.</summary>
    public sealed class PairingStart
    {
        public PairingStart(string code, string pollSecret, string approveUrl,
                            string expiresAt, int intervalSeconds)
        {
            Code = code;
            PollSecret = pollSecret;
            ApproveUrl = approveUrl;
            ExpiresAt = expiresAt;
            IntervalSeconds = intervalSeconds;
        }

        /// <summary>The short code to show in the headset.</summary>
        public string Code { get; }

        /// <summary>Only this device holds it; it is what proves a poll is ours. Never logged.</summary>
        public string PollSecret { get; }

        /// <summary>The address to put in the QR code, for the phone to open.</summary>
        public string ApproveUrl { get; }

        public string ExpiresAt { get; }

        /// <summary>How long to wait between polls. The server sets the pace, not us.</summary>
        public int IntervalSeconds { get; }

        public static PairingStart From(JObject body)
        {
            var interval = Json.OptionalInt(body, "intervalSeconds") ?? 2;
            return new PairingStart(
                Json.RequiredString(body, "code", ""),
                Json.RequiredString(body, "pollSecret", ""),
                Json.RequiredString(body, "approveUrl", ""),
                Json.OptionalString(body, "expiresAt"),
                // A server that asked for zero would have us spin; one second is the floor.
                interval < 1 ? 1 : interval);
        }
    }

    /// <summary>The answer to a poll.</summary>
    public sealed class PairingPoll
    {
        private static readonly IReadOnlyDictionary<string, PairingStatus> Statuses =
            new Dictionary<string, PairingStatus>
            {
                ["pending"] = PairingStatus.Pending,
                ["denied"] = PairingStatus.Denied,
                ["expired"] = PairingStatus.Expired,
                ["approved"] = PairingStatus.Approved,
            };

        public PairingPoll(PairingStatus status, string deviceToken, string userName, string profileName)
        {
            Status = status;
            DeviceToken = deviceToken;
            UserName = userName;
            ProfileName = profileName;
        }

        public PairingStatus Status { get; }

        /// <summary>Set only when approved. Goes to the platform keystore, and is never logged.</summary>
        public string DeviceToken { get; }

        public string UserName { get; }

        /// <summary>The profile the phone chose, or null if it left the choice to the headset.</summary>
        public string ProfileName { get; }

        public static PairingPoll From(JObject body)
        {
            // An unrecognised status is treated as still pending rather than as a failure: the
            // worst case is one more poll, where guessing "expired" would throw away a pairing
            // the person has already approved.
            var status = Json.Enum(body, "status", Statuses, PairingStatus.Pending);
            if (status != PairingStatus.Approved)
                return new PairingPoll(status, null, null, null);

            return new PairingPoll(
                PairingStatus.Approved,
                Json.RequiredString(body, "deviceToken", ""),
                Json.OptionalString(body, "userName"),
                Json.OptionalString(body, "profileName"));
        }
    }
}
