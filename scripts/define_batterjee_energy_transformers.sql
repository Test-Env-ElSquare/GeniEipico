USE [GeniePark];
GO
SET XACT_ABORT ON;
BEGIN TRY
BEGIN TRANSACTION;
IF NOT EXISTS(SELECT 1 FROM Org.Factories WHERE Id=5 AND CustomerId=3 AND IsDeleted=0 AND IsActive=1)
    THROW 51000,'Expected active Batterjee factory 5 / customer 3.',1;
IF NOT EXISTS(SELECT 1 FROM Org.ProductionLines WHERE Id=13 AND FactoryId=5 AND IsDeleted=0 AND IsActive=1)
    THROW 51000,'Expected active line 13 under factory 5.',1;
IF EXISTS(SELECT 1 FROM MasterData.Transformers WITH (UPDLOCK,HOLDLOCK)
    WHERE TransformerName IN ('Bat_Tab1','AS1_L01') AND IsDeleted=0 AND FactoryId<>5)
    THROW 51000,'A source name already belongs to another factory. No definitions were changed.',1;
INSERT MasterData.Transformers(TransformerName,FactoryId,CreatedAt,IsDeleted)
SELECT N.Name,5,DATEADD(hour,3,SYSUTCDATETIME()),0
FROM (VALUES('Bat_Tab1'),('AS1_L01')) N(Name)
WHERE NOT EXISTS(SELECT 1 FROM MasterData.Transformers T WITH (UPDLOCK,HOLDLOCK)
    WHERE T.TransformerName=N.Name AND T.IsDeleted=0);
COMMIT;
SELECT Id,TransformerName,FactoryId FROM MasterData.Transformers
WHERE FactoryId=5 AND TransformerName IN ('Bat_Tab1','AS1_L01') AND IsDeleted=0;
END TRY
BEGIN CATCH
IF @@TRANCOUNT>0 ROLLBACK;
THROW;
END CATCH;
