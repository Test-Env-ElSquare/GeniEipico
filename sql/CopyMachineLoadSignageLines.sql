USE [GenieEipico_Test];
GO
-- Run after AddMachineLoadLineForSignage. This copies scheduler assignments,
-- not Production history. Re-running only fills missing assignments.
UPDATE target
SET LineId = targetLine.Id
FROM Production.MachineMaterialLoads target
JOIN Production.Batches batch ON batch.Id = target.BatchId
JOIN Eipico.RealTime.MachineLoads source ON source.Id = target.SourceRecordId
JOIN Eipico.Definitions.Lines sourceLine ON sourceLine.Id = source.LineId
JOIN Eipico.Definitions.Factories sourceFactory ON sourceFactory.Id = sourceLine.FactoryId
JOIN Org.Factories targetFactory ON targetFactory.CustomerId = batch.CustomerId
    AND REPLACE(targetFactory.Name, ' ', '') = REPLACE(sourceFactory.Name, ' ', '')
JOIN Org.ProductionLines targetLine ON targetLine.FactoryId = targetFactory.Id
    AND targetLine.Name = sourceLine.Name
WHERE target.LineId IS NULL;
SELECT @@ROWCOUNT AS CopiedSchedulerAssignments;