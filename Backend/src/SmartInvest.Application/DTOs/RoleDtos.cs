namespace SmartInvest.Application.DTOs;

public class RoleListItemDto
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public bool IsSystem { get; set; }

    public int UserCount { get; set; }

    public int PermissionCount { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class RoleDetailDto : RoleListItemDto
{
    public List<string> Permissions { get; set; } = [];
}

public class SaveRoleDto
{
    /// <summary>الاسم المعروض بالعربية (مطلوب).</summary>
    public string DisplayName { get; set; } = string.Empty;

    public List<string> Permissions { get; set; } = [];
}

public class PermissionItemDto
{
    public string Key { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;
}

public class PermissionGroupDto
{
    public string Key { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public List<PermissionItemDto> Items { get; set; } = [];
}
