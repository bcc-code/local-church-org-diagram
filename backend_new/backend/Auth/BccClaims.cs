using System.Security.Claims;

namespace Backend.Auth;

/// <summary>
/// Claim names issued by BCC sign-on for the scopes we request (openid, profile, email, church). The OpenID Connect
/// handler keeps the raw names (MapInboundClaims = false), so code reads them as issued.
/// See https://developer.bcc.no/docs/single-signon/get-information-about-the-user
/// </summary>
public static class BccClaims
{
    // openid
    public const string Subject = "sub";
    public const string PersonId = "https://login.bcc.no/claims/personId";
    public const string PersonUid = "https://login.bcc.no/claims/personUid";
    public const string HasMembership = "https://login.bcc.no/claims/hasMembership";

    // profile
    public const string Name = "name";

    // email
    public const string Email = "email";

    // church
    public const string ChurchId = "https://login.bcc.no/claims/churchId";
    public const string ChurchName = "https://login.bcc.no/claims/churchName";

    /// <summary>The person's BCC UID: what memberships and <see cref="Data.Admin"/> rows refer to.</summary>
    public static Guid? GetPersonUid(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(PersonUid), out var uid) ? uid : null;

    public static int? GetPersonId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(PersonId), out var id) ? id : null;

    /// <summary>The person's church: the tenant they read by default.</summary>
    public static long? GetChurchId(this ClaimsPrincipal user) =>
        long.TryParse(user.FindFirstValue(ChurchId), out var id) ? id : null;

    public static bool GetHasMembership(this ClaimsPrincipal user) =>
        bool.TryParse(user.FindFirstValue(HasMembership), out var value) && value;
}
