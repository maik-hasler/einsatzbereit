namespace Api.Authentication;

internal static class AuthorizationPolicies
{
    public const string RealmClaim = "realm";
    
    public const string EinsatzbereitRealm = "einsatzbereit";
    
    public const string AdminRole = "admin";

    public const string DefaultUser = "user";

    public const string OrganisatorRole = "organisator";

    public const string EinsatzbereitAdminPolicy = "einsatzbereit-admin-policy";

    public const string EinsatzbereitDefaultUserPolicy = "einsatzbereit-default-user-policy";

    public const string EinsatzbereitOrganisatorPolicy = "einsatzbereit-organisator-policy";
}