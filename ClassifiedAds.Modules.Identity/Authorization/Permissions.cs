namespace ClassifiedAds.Modules.Identity.Authorization;

public static class Permissions
{
    // Role permissions
    public const string GetRoles = "Permission:GetRoles";
    public const string GetRole = "Permission:GetRole";
    public const string AddRole = "Permission:AddRole";
    public const string UpdateRole = "Permission:UpdateRole";
    public const string DeleteRole = "Permission:DeleteRole";

    // User permissions
    public const string GetUsers = "Permission:GetUsers";
    public const string GetUser = "Permission:GetUser";
    public const string AddUser = "Permission:AddUser";
    public const string UpdateUser = "Permission:UpdateUser";
    public const string SetPassword = "Permission:SetPassword";
    public const string DeleteUser = "Permission:DeleteUser";

    // User management actions
    public const string SendResetPasswordEmail = "Permission:SendResetPasswordEmail";
    public const string SendConfirmationEmailAddressEmail = "Permission:SendConfirmationEmailAddressEmail";
    public const string AssignRole = "Permission:AssignRole";
    public const string RemoveRole = "Permission:RemoveRole";
    public const string LockUser = "Permission:LockUser";
    public const string UnlockUser = "Permission:UnlockUser";
}
