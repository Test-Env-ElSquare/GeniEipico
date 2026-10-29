/*
    Seeds the baseline ASP.NET Core Identity data only.
    Default login: Admin@admin.com / Admin@123
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'GeniePark'
BEGIN
    THROW 51010, 'Safety check failed: select the GeniePark database before running this script.', 1;
END;

BEGIN TRY
    BEGIN TRANSACTION;

    INSERT INTO Security.Roles
        (Name, NormalizedName, ConcurrencyStamp)
    SELECT v.Name, UPPER(v.Name), CONVERT(nvarchar(36), NEWID())
    FROM (VALUES
        ('Admin'),
        ('Supervisor'),
        ('Operator'),
        ('Viewer')
    ) v(Name)
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM Security.Roles r
        WHERE r.NormalizedName = UPPER(v.Name)
    );

    DECLARE @adminCustomerId int =
    (
        SELECT TOP (1) c.Id
        FROM Org.Customers c
        WHERE c.IsActive = 1
          AND c.IsDeleted = 0
        ORDER BY c.Id
    );

    IF @adminCustomerId IS NULL
        THROW 51001, 'An active customer is required before the default admin can be created.', 1;

    IF NOT EXISTS
    (
        SELECT 1
        FROM Security.Users u
        WHERE u.NormalizedUserName = 'ADMIN@ADMIN.COM'
           OR u.NormalizedEmail = 'ADMIN@ADMIN.COM'
    )
    BEGIN
        INSERT INTO Security.Users
        (
            CustomerId,
            FactoryId,
            LineId,
            FullName,
            IsActive,
            LastLoginAt,
            UserName,
            NormalizedUserName,
            Email,
            NormalizedEmail,
            EmailConfirmed,
            PasswordHash,
            SecurityStamp,
            ConcurrencyStamp,
            PhoneNumber,
            PhoneNumberConfirmed,
            TwoFactorEnabled,
            LockoutEnd,
            LockoutEnabled,
            AccessFailedCount
        )
        VALUES
        (
            @adminCustomerId,
            NULL,
            NULL,
            'System Administrator',
            1,
            NULL,
            'Admin@admin.com',
            'ADMIN@ADMIN.COM',
            'Admin@admin.com',
            'ADMIN@ADMIN.COM',
            1,
            'AQAAAAIAAYagAAAAECucBuR1gFGmEp9ncN47bICqKunvBtiz2ZMmXa82FccsibsENbpGRVP7/LgFAXyYQw==',
            REPLACE(CONVERT(nvarchar(36), NEWID()), '-', ''),
            CONVERT(nvarchar(36), NEWID()),
            NULL,
            0,
            0,
            NULL,
            1,
            0
        );
    END;

    DECLARE @adminUserId int =
    (
        SELECT TOP (1) u.Id
        FROM Security.Users u
        WHERE u.NormalizedUserName = 'ADMIN@ADMIN.COM'
        ORDER BY u.Id
    );

    DECLARE @adminRoleId int =
    (
        SELECT TOP (1) r.Id
        FROM Security.Roles r
        WHERE r.NormalizedName = 'ADMIN'
        ORDER BY r.Id
    );

    IF @adminUserId IS NULL OR @adminRoleId IS NULL
        THROW 51002, 'The default admin user or Admin role could not be resolved.', 1;

    IF NOT EXISTS
    (
        SELECT 1
        FROM Security.UserRoles ur
        WHERE ur.UserId = @adminUserId
          AND ur.RoleId = @adminRoleId
    )
    BEGIN
        INSERT INTO Security.UserRoles (UserId, RoleId)
        VALUES (@adminUserId, @adminRoleId);
    END;

    COMMIT TRANSACTION;

    SELECT
        'Identity seed completed' AS [Status],
        @adminCustomerId AS AdminCustomerId,
        @adminUserId AS AdminUserId,
        (SELECT COUNT(*) FROM Security.Users) AS Users,
        (SELECT COUNT(*) FROM Security.Roles) AS Roles,
        (SELECT COUNT(*) FROM Security.UserRoles) AS UserRoles;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;

