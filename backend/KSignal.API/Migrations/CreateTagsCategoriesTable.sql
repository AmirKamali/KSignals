-- MySQL script to create TagsCategories table
-- This table stores tags organized by series categories from Kalshi API

CREATE TABLE IF NOT EXISTS `TagsCategories` (
    `Id` INT NOT NULL AUTO_INCREMENT,
    `Category` VARCHAR(255) NOT NULL,
    `Tag` VARCHAR(255) NOT NULL,
    `LastUpdate` DATETIME(6) NOT NULL,
    `IsDeleted` TINYINT(1) NOT NULL DEFAULT 0,
    PRIMARY KEY (`Id`),
    UNIQUE INDEX `IX_TagsCategories_Category_Tag` (`Category`, `Tag`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Optional: Add index on IsDeleted for filtering deleted records
CREATE INDEX `IX_TagsCategories_IsDeleted` ON `TagsCategories` (`IsDeleted`);

-- Optional: Add index on LastUpdate for sorting/filtering by update time
CREATE INDEX `IX_TagsCategories_LastUpdate` ON `TagsCategories` (`LastUpdate`);



