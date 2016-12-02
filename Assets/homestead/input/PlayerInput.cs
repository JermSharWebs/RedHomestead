﻿using UnityEngine;
using System.Collections;
using System;
using System.Collections.Generic;
using RedHomestead.Rovers;
using RedHomestead.Construction;

/// <summary>
/// Responsible for raycasting, modes, and gameplay input
/// </summary>
public class PlayerInput : MonoBehaviour {
    public enum PlanningMode { Default, Exterior, Interiors }
    public static PlayerInput Instance;

    public Camera FlowCamera;
    public Light Headlamp1, Headlamp2;
    /// <summary>
    /// Tube prefab to be created when linking bulkheads
    /// </summary>
    public Transform tubePrefab, gasPipePrefab, powerlinePrefab;
    /// <summary>
    /// the FPS input script (usually on the parent transform)
    /// </summary>
    public CustomFPSController FPSController;
    /// <summary>
    /// The prefab for a construction zone
    /// </summary>
    public Transform ConstructionZonePrefab,
        //one of these for each module does NOT scale
        SmallSolarFarmPrefab,
        SmallGasTankPrefab,
        OxygenTank,
        SabatierPrefab,
        OreExtractorPrefab;
    /// <summary>
    /// the material to put on module prefabs
    /// when planning where to put them on the ground
    /// </summary>
    public Material translucentPlanningMat;

    internal Module PlannedModule = Module.Unspecified;
    internal PlanningMode CurrentMode = PlanningMode.Default;
    internal PlanningMode AvailableMode = PlanningMode.Exterior;

    /// <summary>
    /// Visualization == transparent preview of module to be built
    /// Cache == only create 1 of each type of module because creation is expensive
    /// </summary>
    private Dictionary<Module, Transform> VisualizationCache = new Dictionary<Module, Transform>();
    private RoverInput DrivingRoverInput;
    private Collider selectedAirlock1, selectedGasValve, selectedPowerSocket, carriedObject;
    private Compound selectedCompound = Compound.Unspecified;
    private List<Transform> createdTubes = new List<Transform>();
    private List<Transform> createdPipes = new List<Transform>();
    private List<Transform> createdPowerlines = new List<Transform>();
    private bool playerIsOnFoot = true;

    private bool playerInVehicle
    {
        get
        {
            return !playerIsOnFoot;
        }
    }
    private Transform PlannedModuleVisualization;
    private Transform lastHobbitHoleTransform;
    private HobbitHole lastHobbitHole;

    void Awake()
    {
        Instance = this;
    }

	// Update is called once per frame
	void Update () {

	    if (Input.GetKeyUp(KeyCode.Escape))
        {
            if (playerIsOnFoot)
            {
                Application.Quit();
            }
            else if (playerInVehicle)
            {
                ToggleVehicle(null);
            }
        }

        if (Input.GetKeyUp(KeyCode.Tab))
        {
            CycleMode();
        }

        if (Input.GetKeyUp(KeyCode.F))
        {
            Headlamp1.enabled = Headlamp2.enabled = !Headlamp1.enabled;
        }

        if (Input.GetKeyUp(KeyCode.V))
        {
            FlowCamera.enabled = !FlowCamera.enabled;
        }


        PromptInfo newPrompt = null;
        bool doInteract = Input.GetKeyUp(KeyCode.E);
        RaycastHit hitInfo;
        if (CurrentMode == PlanningMode.Exterior)
        {
            if (PlannedModule == Module.Unspecified)
            {
                if (Input.GetKeyUp(KeyCode.Q))
                {
                    GuiBridge.Instance.CycleConstruction(-1);
                }
                else if (Input.GetKeyUp(KeyCode.Z))
                {
                    GuiBridge.Instance.CycleConstruction(1);
                }
                else if (Input.GetKeyUp(KeyCode.Alpha1))
                {
                    GuiBridge.Instance.SelectConstructionPlan(0);
                }
                else if (Input.GetKeyUp(KeyCode.Alpha2))
                {
                    GuiBridge.Instance.SelectConstructionPlan(1);
                }
                else if (Input.GetKeyUp(KeyCode.Alpha3))
                {
                    GuiBridge.Instance.SelectConstructionPlan(2);
                }
                else if (Input.GetKeyUp(KeyCode.Alpha4))
                {
                    GuiBridge.Instance.SelectConstructionPlan(3);
                }
            }

            if (PlannedModuleVisualization != null)
            {
                if (Input.GetMouseButton(0))
                {
                    PlannedModuleVisualization.Rotate(Vector3.up * 90 * Time.deltaTime);
                }
                else if (Input.GetMouseButton(1))
                {
                    PlannedModuleVisualization.Rotate(-Vector3.up * 90 * Time.deltaTime);
                }
            }

            if (Physics.Raycast(new Ray(this.transform.position, this.transform.forward), out hitInfo, 300f, LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore))
            {
                if (hitInfo.collider != null)
                {
                    if (hitInfo.collider.CompareTag("terrain"))
                    {
                        if (CurrentMode == PlanningMode.Exterior && PlannedModuleVisualization != null)
                        {
                            //TODO: raycast 3 more times (other 3 corners)
                            //then take the average height between them
                            //and invalidate the placement if it passes some threshold
                            PlannedModuleVisualization.position = hitInfo.point;

                            if (doInteract)
                            {
                                PlaceConstructionHere(hitInfo.point);
                            }
                            else
                            {
                                newPrompt = Prompts.PlanConstructionZoneHint;
                            }
                        }
                    }
                }
            }
        }
        else if (Physics.Raycast(new Ray(this.transform.position, this.transform.forward), out hitInfo, 300f, LayerMask.GetMask("interaction"), QueryTriggerInteraction.Collide))
        {
            if (hitInfo.collider != null)
            {
                if (hitInfo.collider.gameObject.CompareTag("movable"))
                {
                    if (carriedObject == null)
                    {
                        if (doInteract)
                        {
                            PickUpObject(hitInfo);
                        }
                        else
                        {
                            newPrompt = Prompts.PickupHint;
                        }
                    }
                    else
                    {
                        if (doInteract)
                        {
                            DropObject();
                        }
                        else
                        {
                            newPrompt = Prompts.DropHint;
                        }
                    }
                }
                else if (hitInfo.collider.gameObject.CompareTag("bulkhead"))
                {
                    newPrompt = OnBulkhead(newPrompt, doInteract, hitInfo);
                }
                else if (hitInfo.collider.gameObject.CompareTag("powerplug"))
                {
                    newPrompt = OnPowerPlug(newPrompt, doInteract, hitInfo);
                }
                else if (IsGasValve(hitInfo.collider))
                {
                    newPrompt = OnGasValve(newPrompt, doInteract, hitInfo);
                }
                else if (playerIsOnFoot && hitInfo.collider.gameObject.CompareTag("rover"))
                {
                    if (doInteract)
                    {
                        ToggleVehicle(hitInfo.collider.transform.GetComponent<RoverInput>());
                    }
                    else
                    {
                        newPrompt = Prompts.DriveRoverPrompt;
                    }
                } 
                else if (hitInfo.collider.CompareTag("constructionzone"))
                {
                    ConstructionZone zone = hitInfo.collider.GetComponent<ConstructionZone>();

                    if (zone != null && carriedObject == null && zone.CanConstruct)
                    {
                        if (doInteract)
                        {
                            zone.WorkOnConstruction();
                        }
                        else
                        {
                            newPrompt = Prompts.ConstructHint;
                        }
                    }
                }
                else if (hitInfo.collider.CompareTag("door"))
                {
                    if (hitInfo.collider.gameObject.name == Airlock.OpenDoorName)
                    {
                        if (doInteract)
                        {
                            Airlock.ToggleDoor(hitInfo.collider.transform);
                        }
                        else
                        {
                            newPrompt = Prompts.DoorHint;
                        }
                    }
                    else
                    {
                        newPrompt = Prompts.DoorLockedHint;
                    }
                }
                else if (hitInfo.collider.CompareTag("cavernwall"))
                {
                    if (doInteract)
                    {
                        if (hitInfo.collider.transform.parent == lastHobbitHoleTransform)
                        {
                            lastHobbitHole.Excavate(hitInfo.collider.transform.localPosition);
                        }
                        else
                        {
                            HobbitHole hh = hitInfo.collider.transform.parent.GetComponent<HobbitHole>();

                            if (hh != null)
                                lastHobbitHole = hh;

                            lastHobbitHole.Excavate(hitInfo.collider.transform.localPosition);
                        }
                    }
                    else
                    {
                        //prompt
                    }
                }
                else if (hitInfo.collider.CompareTag("button"))
                {
                    if (doInteract)
                    {
                        if (hitInfo.collider.name == "depressurize")
                        {
                            hitInfo.collider.transform.parent.GetComponent<Airlock>().Depressurize();
                        }
                        else if (hitInfo.collider.name == "pressurize")
                        {
                            hitInfo.collider.transform.parent.GetComponent<Airlock>().Pressurize();
                        }
                    }
                    else
                    {
                        newPrompt = Prompts.GenericButtonHint;
                    }
                }
                else if (hitInfo.collider.CompareTag("water"))
                {
                    if (doInteract)
                    {
                        SurvivalTimer.Instance.FillWater();
                    }
                    else
                    {
                        newPrompt = Prompts.DrinkWaterHint;
                    }
                }
                else if (hitInfo.collider.CompareTag("foodprep"))
                {
                    if (doInteract)
                    {
                    }
                    else
                    {
                        newPrompt = Prompts.FoodPrepHint;
                    }
                }
            }
            else if (doInteract)
            {
                if (selectedAirlock1 != null)
                {
                    selectedAirlock1 = null;
                }
                if (selectedGasValve != null)
                {
                    selectedGasValve = null;
                }
            }
        }
        //if we raycast, and DO NOT hit our carried object, it has gotten moved because of physics
        //so drop it!
        else if (carriedObject != null)
        {
            DropObject();
        }

        //if we were hovering or doing something that has a prompt
        //we will have a newPrompt
        //if we don't
        if (newPrompt == null)
        {
            GuiBridge.Instance.HidePrompt();
        }
        else
        {
            GuiBridge.Instance.ShowPrompt(newPrompt);
        }
	}

    private bool IsGasValve(Collider collider)
    {
        return collider.CompareTag("valve") ||
            collider.CompareTag("hydrogenvalve") ||
            collider.CompareTag("oxygenvalve") ||
            collider.CompareTag("methanevalve") ||
            collider.CompareTag("watervalve") ||
            collider.CompareTag("carbondioxidevalve");
    }

    private PromptInfo OnPowerPlug(PromptInfo newPrompt, bool doInteract, RaycastHit hitInfo)
    {
        return OnLinkable(doInteract, hitInfo, selectedPowerSocket, value => selectedPowerSocket = value, PlacePowerPlug, Prompts.PowerPlugPrompts);
    }

    private PromptInfo OnBulkhead(PromptInfo newPrompt, bool doInteract, RaycastHit hitInfo)
    {
        return OnLinkable(doInteract, hitInfo, selectedAirlock1, value => selectedAirlock1 = value, PlaceTube, Prompts.BulkheadBridgePrompts);
    }

    private PromptInfo OnGasValve(PromptInfo newPrompt, bool doInteract, RaycastHit hitInfo)
    {
        Compound other = GetCompoundFromValve(hitInfo.collider);

        if (selectedCompound != Compound.Unspecified)
        {
            if (!CompoundsMatch(selectedCompound, other))
            {
                return Prompts.InvalidPipeHint;
            }
        }

        return OnLinkable(doInteract, hitInfo, selectedGasValve, value => 
        {
            selectedGasValve = value;

            if (value == null)
                //todo: bug this actually should be nullable
                selectedCompound = Compound.Unspecified;
            else 
                selectedCompound = other;

            print("selected " +selectedCompound.ToString());
        }, PlaceGasPipe, Prompts.GasPipePrompts);
    }

    private static bool CompoundsMatch(Compound selectedCompound, Compound other)
    {
        if (selectedCompound == Compound.Unspecified &&
            other == Compound.Unspecified)
        {
            return true;
        }
        else if (selectedCompound != Compound.Unspecified &&
            other == Compound.Unspecified)
        {
            return true;
        }
        else
        {
            return selectedCompound == other;
        }
    }

    private static Compound GetCompoundFromValve(Collider collider)
    {
        switch (collider.tag)
        {
            case "oxygenvalve":
                return Compound.Oxygen;
            case "hydrogenvalve":
                return Compound.Hydrogen;
            case "methanevalve":
                return Compound.Methane;
            case "carbondioxidevalve":
                return Compound.CarbonDioxide;
            case "watervalve":
                return Compound.Water;
            default:
                return Compound.Unspecified;
        }
    }

    //todo: move out of this class
    public static string GetValveFromCompound(Compound c)
    {
        switch (c)
        {
            case Compound.Oxygen:
                return "oxygenvalve";
            case Compound.Hydrogen:
                return "hydrogenvalve";
            case Compound.Methane:
                return "methanevalve";
            case Compound.CarbonDioxide:
                return "carbondioxidevalve";
            case Compound.Water:
                return "watervalve";
            default:
                return "valve";
        }
    }

    private static PromptInfo OnLinkable(bool doInteract, RaycastHit hitInfo, Collider savedLinkEnd, Action<Collider> SetSaved, Action<Collider> OnLinkPlaced, LinkablePrompts promptGroup )
    {
        PromptInfo newPrompt = null;

        if (doInteract)
        {
            if (savedLinkEnd == null)
            {
                SetSaved(hitInfo.collider);
                //maybe not this? maybe null?
                //maybe a prompt instead of a hint
                newPrompt = promptGroup.HoverWhenOneSelected;
            }
            else if (savedLinkEnd != hitInfo.collider)
            {
                OnLinkPlaced(hitInfo.collider);
                SetSaved(null);
                newPrompt = promptGroup.WhenCompleted;
            }
        }
        else
        {
            if (savedLinkEnd == null)
            {
                newPrompt = promptGroup.HoverWhenNoneSelected;
            }
            else if (savedLinkEnd != hitInfo.collider)
            {
                newPrompt = promptGroup.HoverWhenOneSelected;
            }
        }

        return newPrompt;
    }

    private void PlaceConstructionHere(Vector3 point)
    {
        Transform zoneT = (Transform)GameObject.Instantiate(ConstructionZonePrefab, PlannedModuleVisualization.position, PlannedModuleVisualization.rotation);

        ConstructionZone zone = zoneT.GetComponent<ConstructionZone>();
        zone.UnderConstruction = PlannedModule;
        zone.ModulePrefab = GetPlannedModulePrefab();

        zone.InitializeRequirements();

        CycleMode();
    }

    private Transform GetPlannedModulePrefab()
    {
        switch(PlannedModule)
        {
            //storage
            case Module.SmallGasTank:
                return SmallGasTankPrefab;
            case Module.LargeGasTank:
                return OxygenTank;
            //extraction
            case Module.SabatierReactor:
                return SabatierPrefab;
            case Module.OreExtractor:
                return OreExtractorPrefab;
            //power
            case Module.SolarPanelSmall:
                return SmallSolarFarmPrefab;
            default:
                return SmallSolarFarmPrefab;
        }
    }

    private void CycleMode()
    {
        //todo: fix lazy code
        if (CurrentMode == PlanningMode.Default)
            CurrentMode = this.AvailableMode;
        else
            CurrentMode = PlanningMode.Default;

        switch(CurrentMode)
        {
            case PlanningMode.Exterior:
                FlowCamera.cullingMask = 1 << 10;
                FlowCamera.enabled = true;
                Cursor.lockState = CursorLockMode.Confined;
                Cursor.visible = true;
                break;
            case PlanningMode.Default:
                FlowCamera.enabled = false;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                if (this.PlannedModuleVisualization != null)
                {
                    this.PlannedModuleVisualization.gameObject.SetActive(false);
                    this.PlannedModuleVisualization = null;
                }
                this.PlannedModule = Module.Unspecified;
                break;
            case PlanningMode.Interiors:
                FlowCamera.cullingMask = 1 << 10;
                FlowCamera.enabled = true;
                break;
        }

        GuiBridge.Instance.RefreshMode();
    }

    private void PickUpObject(RaycastHit hitInfo)
    {
        carriedObject = hitInfo.collider;
        carriedObject.GetComponent<Rigidbody>().useGravity = false;
        carriedObject.transform.SetParent(this.transform);
    }

    private void DropObject()
    {
        carriedObject.GetComponent<Rigidbody>().useGravity = true;
        carriedObject.transform.SetParent(null);
        carriedObject = null;
    }

    private void ToggleVehicle(RoverInput roverInput)
    {
        if (roverInput == null && DrivingRoverInput != null)
        {
            playerIsOnFoot = true;
            DrivingRoverInput.enabled = false;
            FPSController.transform.position = DrivingRoverInput.transform.Find("Exit").transform.position;
            FPSController.transform.SetParent(null);
            FPSController.SuspendInput = false;
        }
        else
        {
            playerIsOnFoot = false;
            //FPSController.enabled = false;
            DrivingRoverInput = roverInput;
            DrivingRoverInput.enabled = true;
            FPSController.transform.SetParent(DrivingRoverInput.transform.Find("Enter").transform);
            FPSController.transform.localPosition = Vector3.zero;
            FPSController.transform.localRotation = Quaternion.identity;
            FPSController.SuspendInput = true;
        }
    }

    private void PlaceTube(Collider collider)
    {
        PlaceRuntimeLinkingObject(selectedAirlock1, collider, tubePrefab, createdTubes, true, .2f);
    }

    private void PlaceGasPipe(Collider collider)
    {
        Transform newPipe = PlaceRuntimeLinkingObject(selectedGasValve, collider, gasPipePrefab, createdPipes);

        ModuleGameplay g1 = selectedGasValve.transform.root.GetComponent<ModuleGameplay>(), g2 = collider.transform.root.GetComponent<ModuleGameplay>();
        if (g1 != null && g2 != null)
        {
            g1.LinkToModule(g2);
            g2.LinkToModule(g1);
            if (g2 is GasStorage)
            {
                (g2 as GasStorage).SpecifyCompound(selectedCompound);
            }
        }

        Pipe pipeScript = newPipe.GetComponent<Pipe>();
        pipeScript.PipeType = selectedCompound;
    }

    private void PlacePowerPlug(Collider collider)
    {
        PlaceRuntimeLinkingObject(selectedPowerSocket, collider, powerlinePrefab, createdPowerlines);

        //turn on "plug" cylinders
        collider.transform.GetChild(0).GetComponent<MeshRenderer>().enabled = true;
        selectedPowerSocket.transform.GetChild(0).GetComponent<MeshRenderer>().enabled = true;

        ModuleGameplay g1 = selectedPowerSocket.transform.root.GetComponent<ModuleGameplay>(), g2 = collider.transform.root.GetComponent<ModuleGameplay>();
        if (g1 != null && g2 != null)
        {
            if (g1.HasPower || g2.HasPower)
            {
                g1.HasPower = g2.HasPower = true;
            }
        }
    }

    private static Transform PlaceRuntimeLinkingObject(Collider firstObject, Collider otherObject, Transform linkingObjectPrefab, List<Transform> addToList, bool hideObjectEnds = false, float extraScale = 0f)
    {
        float distanceBetween = Vector3.Distance(firstObject.transform.position, otherObject.transform.position);

        Vector3 midpoint = Vector3.Lerp(firstObject.transform.position, otherObject.transform.position, 0.5f);
        Transform newTube = GameObject.Instantiate<Transform>(linkingObjectPrefab);

        newTube.position = midpoint;
        newTube.LookAt(otherObject.transform);
        newTube.localScale = new Vector3(newTube.localScale.x, newTube.localScale.y, (distanceBetween / 2f) + extraScale);
        addToList.Add(newTube);

        if (hideObjectEnds)
        {
            firstObject.gameObject.SetActive(false);
            otherObject.gameObject.SetActive(false);
        }

        return newTube;
    }

    internal void PlanModule(Module planModule)
    {
        this.PlannedModule = planModule;

        if (VisualizationCache.ContainsKey(planModule))
        {
            PlannedModuleVisualization = VisualizationCache[planModule];
            PlannedModuleVisualization.gameObject.SetActive(true);
        }
        else
        {
            PlannedModuleVisualization = GameObject.Instantiate<Transform>(GetPlannedModulePrefab());
            VisualizationCache[planModule] = PlannedModuleVisualization;
            RecurseDisableColliderSetTranslucentRenderer(PlannedModuleVisualization);
        }        
    }

    private void RecurseDisableColliderSetTranslucentRenderer(Transform parent)
    {
        foreach (Transform child in parent)
        {
            //only default layer
            if (child.gameObject.layer == 0)
            {
                Collider c = child.GetComponent<Collider>();
                if (c != null)
                    c.enabled = false;

                Renderer r = child.GetComponent<Renderer>();
                if (r != null)
                {
                    if (r.materials != null && r.materials.Length > 1)
                    {
                        var newMats = new Material[r.materials.Length];
                        for (int i = 0; i < r.materials.Length; i++)
                        {
                            newMats[i] = translucentPlanningMat;
                        }
                        r.materials = newMats;
                    }
                    else
                    {
                        r.material = translucentPlanningMat;
                    }
                }
            }

            RecurseDisableColliderSetTranslucentRenderer(child);
        }
    }

    public void KillPlayer()
    {
        GuiBridge.Instance.ShowKillMenu();
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.Confined;
        FPSController.enabled = false;
        this.enabled = false;
    }
}
