CREATE TABLE [dbo].[AppCache] (
    [Id] NVARCHAR(449) NOT NULL,
    [Value] VARBINARY(MAX) NOT NULL,
    [ExpiresAtTime] DATETIMEOFFSET NOT NULL,
    [SlidingExpirationInSeconds] BIGINT NULL,
    [AbsoluteExpiration] DATETIMEOFFSET NULL,
    PRIMARY KEY ([Id]),
    INDEX [Index_ExpiresAtTime] NONCLUSTERED ([ExpiresAtTime])
);