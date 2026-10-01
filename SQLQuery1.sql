DECLARE @UserId NVARCHAR(450);
SELECT @UserId = Id FROM AspNetUsers WHERE Email = 'khamampho71@gmail.com';

DELETE FROM AspNetUserRoles WHERE UserId = @UserId;
DELETE FROM AspNetUsers WHERE Id = @UserId;

DECLARE @UserId NVARCHAR(450);
SELECT @UserId = Id FROM AspNetUsers WHERE Email = 'neoalicekhama.com';

DELETE FROM AspNetUserRoles WHERE UserId = @UserId;
DELETE FROM AspNetUsers WHERE Id = @UserId;