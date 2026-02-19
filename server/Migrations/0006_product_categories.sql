CREATE TABLE IF NOT EXISTS ProductCategories (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL UNIQUE,
    Active INTEGER NOT NULL DEFAULT 1,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL
);

INSERT INTO ProductCategories(Name, Active, CreatedAt, UpdatedAt)
SELECT DISTINCT Category, 1, datetime('now'), datetime('now')
FROM Products
WHERE Category IS NOT NULL AND trim(Category) <> ''
AND Category NOT IN (SELECT Name FROM ProductCategories);
