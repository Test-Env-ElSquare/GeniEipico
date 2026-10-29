namespace API.Common;

/// <summary>
/// Permission names retained from the Eipico application. They are stored as
/// Genie role claims (claim type <c>permissions</c>) and are evaluated by
/// <see cref="RequiresPermissionAttribute"/>.
/// </summary>
public static class EipicoPermissions
{
    public const string Everything = "HasAccessToEverything";
    public const string GetAllProductPlans = "CanAccessGetAllProductPlans";
    public const string PairedBatches = "CanAccessPairedBatches";
    public const string AddMaterial = "HasAccessToAddMaterial";
    public const string BatchHistory = "HasAccessToBatchHistory";
    public const string BatchSchedulerView = "HasAccessToBatchSchedulerView";
    public const string BatchSchedulerActivate = "HasAccessToBatchSchedulerViewAndActivate";
    public const string BatchSchedulerFinish = "HasAccessToBatchSchedulerViewAndFinish";
    public const string BatchSettings = "HasAccessToBatchSettings";
    public const string BatchSettingsDelete = "HasAccessToBatchSettingsAndDelete";
    public const string BatchWeight = "HasAccessToBatchWeight";
    public const string BeaconReport = "HasAccessToBeconReport";
    public const string BykonState = "HasAccessToBykonState";
    public const string DeleteMaterial = "HasAccessToDeleteMaterial";
    public const string EditMaterial = "HasAccessToEditMaterial";
    public const string EnergyReport = "HasAccessToEnergyReport";
    public const string Factories = "HasAccessToFactories";
    public const string HistoricalDashboards = "HasAccessToHistoricalDashboards";
    public const string LineMachine = "HasAccessToLineMachine";
    public const string MachineLoadDetails = "HasAccessToMachineLoadDetails";
    public const string MachineStatus = "HasAccessToMachineStatus";
    public const string MainDashboards = "HasAccessToMainDashboards";
    public const string MaterialControl = "HasAccessToMaterialControl";
    public const string ProductionPlan = "HasAccessToProductionPlan";
    public const string ProductionPlanAdd = "HasAccessToProductionPlanAndAdd";
    public const string ProductionPlanDelete = "HasAccessToProductionPlanAndDelete";
    public const string ProductionPlanEdit = "HasAccessToProductionPlanAndEdit";
    public const string Scada = "HasAccessToscada";
    public const string ScaleDetails = "HasAccessToScaleDetails";
    public const string ScaleHistory = "HasAccessToScaleHistory";
    public const string SendEmail = "HasAccessToSendEmail";
    public const string Skus = "HasAccessToSKUs";
    public const string StoppagePlan = "HasAccessToStoppagePlan";
    public const string StoppageReasons = "HasAccessToStopPageReasons";
    public const string UserManagement = "HasAccessToUserManagement";

    public const string ClaimType = "permissions";
}
