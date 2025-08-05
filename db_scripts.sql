-- 1. 用户信息表
CREATE TABLE [dbo].[Users] (
    [Id] INT IDENTITY(1,1) PRIMARY KEY,  --用户主键，自增
    [UserName] NVARCHAR(100) NOT NULL UNIQUE, --用户名，唯一
    [PasswordHash] NVARCHAR(256) NOT NULL, --密码，加密存储
    [IsAdmin] BIT NOT NULL DEFAULT(0), --是否为管理员
    [IsApproved] BIT NOT NULL DEFAULT(0), --是否通过审核
    [RegisterTime] DATETIME NOT NULL DEFAULT(GETDATE()), --注册时间
    [Email] NVARCHAR(200) NOT NULL DEFAULT('') --邮箱
);

--2.图片表
CREATE TABLE [dbo].[Image] (
    [Id] INT IDENTITY(1,1) PRIMARY KEY,         -- 图片主键，自增
    [UserId] INT NOT NULL,                      -- 上传用户的ID，外键
    [FileName] NVARCHAR(255) NOT NULL,          -- 原始文件名
    [FilePath] NVARCHAR(500) NOT NULL,          -- 图片在服务器上的存储路径
    [UploadTime] DATETIME NOT NULL,             -- 上传时间
    [FileSize] BIGINT NOT NULL,                 -- 文件大小（字节）
    [FileType] NVARCHAR(50) NOT NULL,           -- 文件类型（如 image/png）
    [ImageSource] NVARCHAR(500) NULL,           -- 图片来源说明（如：网络下载、手机拍摄等）
    [ImageCategory] NVARCHAR(100) NULL,         -- 图片类型说明（如：风景、人物、动物等）
    [ImageUrl] NVARCHAR(500) NULL,              -- 图片外链地址（用于分享）
    [IsDeleted] BIT NOT NULL DEFAULT 0,         -- 是否已删除（软删除）
    CONSTRAINT FK_Image_User FOREIGN KEY ([UserId]) REFERENCES [Users]([Id])
);


-- 3. 管理员初始账号插入（请将PasswordHash替换为实际加密后的值）
INSERT INTO [dbo].[Users] ([UserName], [PasswordHash], [IsAdmin], [IsApproved], [Email])
VALUES ('Nikii', 'z5seyyjpTQMV59tagCmhzF3nppRpg29KgzFwvAcGIzY=', 1, 1, 'ivringmk@foxmail.com');


ALTER TABLE [dbo].[Image]
ADD CONSTRAINT FK_Image_Folder FOREIGN KEY ([FolderId]) REFERENCES [ImageFolder]([Id]);

-- 4. 图片表（更新后的完整结构）
-- 删除旧的外键约束（如果存在）
IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Image_User')
BEGIN
    ALTER TABLE [dbo].[Image] DROP CONSTRAINT FK_Image_User
END

-- 删除旧的Image表（如果存在）
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Image')
BEGIN
    DROP TABLE [dbo].[Image]
END

-- 重新创建Image表
CREATE TABLE [dbo].[Image] (
    [Id] INT IDENTITY(1,1) PRIMARY KEY,         -- 图片主键，自增
    [UserId] INT NOT NULL,                      -- 上传用户的ID，外键
    [FileName] NVARCHAR(255) NOT NULL,          -- 原始文件名
    [FilePath] NVARCHAR(500) NOT NULL,          -- 图片在服务器上的存储路径
    [UploadTime] DATETIME NOT NULL,             -- 上传时间
    [FileSize] BIGINT NOT NULL,                 -- 文件大小（字节）
    [FileType] NVARCHAR(50) NOT NULL,           -- 文件类型（如 image/png）
    [ImageSource] NVARCHAR(500) NULL,           -- 图片来源说明（如：网络下载、手机拍摄等）
    [ImageCategory] NVARCHAR(100) NULL,         -- 图片类型说明（如：风景、人物、动物等）
    [ImageUrl] NVARCHAR(500) NULL,              -- 图片外链地址（用于分享）
    [IsDeleted] BIT NOT NULL DEFAULT 0,         -- 是否已删除（软删除）
    CONSTRAINT FK_Image_User FOREIGN KEY ([UserId]) REFERENCES [Users]([Id])
); 

-- 5. 文件夹表
CREATE TABLE [dbo].[ImageFolder] (
    [Id] INT IDENTITY(1,1) PRIMARY KEY,         -- 文件夹主键，自增
    [UserId] INT NOT NULL,                      -- 所属用户ID
    [FolderName] NVARCHAR(100) NOT NULL,        -- 文件夹名称
    [IsShared] BIT NOT NULL DEFAULT 0,          -- 是否共享（0-私有，1-共享）
    [CreateTime] DATETIME NOT NULL DEFAULT(GETDATE()), -- 创建时间
    CONSTRAINT FK_ImageFolder_User FOREIGN KEY ([UserId]) REFERENCES [Users]([Id])
);

-- 6. 图片表增加文件夹ID字段
ALTER TABLE [dbo].[Image]
ADD [FolderId] INT NULL; -- 所属文件夹ID，可为空

-- 7. 图片存储方式配置表
CREATE TABLE [dbo].[StorageSetting] (
    [Id] INT IDENTITY(1,1) PRIMARY KEY, -- 主键
    [StorageType] NVARCHAR(50) NOT NULL, -- 'File' 或 'Database'
    [UpdateTime] DATETIME NOT NULL DEFAULT(GETDATE()) -- 更新时间
);
-- 初始化为文件夹存储
INSERT INTO [dbo].[StorageSetting] ([StorageType]) VALUES ('File');

-- 8. 图片表增加二进制内容字段
ALTER TABLE [dbo].[Image]
ADD [Data] VARBINARY(MAX) NULL; -- 用于数据库存储图片内容

-- 9. 营业执照识别结果表
-- 10. 发票识别结果表

-- 后续所有数据库相关 SQL 都写在本文件中