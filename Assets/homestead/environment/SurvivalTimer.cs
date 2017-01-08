﻿using UnityEngine;
using System.Collections;
using System;

namespace RedHomestead
{
    public static class Constants
    {
        public const float KilogramsOxygenPerHour = 0.0972f;
        public const float CaloriesPerDay = 2400;
        public const float LitersOfWaterPerDay = 3f;
    }
}


[Serializable]
public abstract class SurvivalResource
{
    public float CurrentAmount = 100f;
    public float MaximumAmount = 100f;
    public float ConsumptionPerSecond = .1f;
    public int HoursLeftHint
    {
        get
        {
            int hours = (int)Math.Ceiling(CurrentAmount / (ConsumptionPerSecond * 60));

            return hours > 2 ? -1 : hours;
        }
    }
    public abstract void Consume();

    public abstract void ResetToMaximum();
}

public class SingleSurvivalResource : SurvivalResource
{
    public override void Consume()
    {
        CurrentAmount -= Time.deltaTime * ConsumptionPerSecond;
        this.UpdateUI(CurrentAmount / MaximumAmount, HoursLeftHint);
    }

    public override void ResetToMaximum()
    {
        CurrentAmount = MaximumAmount;
        this.UpdateUI(1f, -1);
    }
    /// <summary>
    /// external call to UI code, float parameter is percentage of resource, 0-1f
    /// </summary>
    internal Action<float, int> UpdateUI;

    internal void Increment(float amount)
    {
        CurrentAmount += amount;

        if (CurrentAmount > MaximumAmount)
            CurrentAmount = MaximumAmount;
        else if (CurrentAmount < 0)
            CurrentAmount = 0f;

        this.UpdateUI(CurrentAmount / MaximumAmount, HoursLeftHint);
    }
}

public class DoubleSurvivalResource : SurvivalResource
{
    public bool IsOnLastBar = false;

    public override void Consume()
    {
        CurrentAmount -= Time.deltaTime * ConsumptionPerSecond;
        
        if (IsOnLastBar)
            this.UpdateUI(0f, CurrentAmount / MaximumAmount, HoursLeftHint);

        else if (CurrentAmount <= 0f)
        {
            IsOnLastBar = true;
            CurrentAmount = MaximumAmount;
            this.UpdateUI(CurrentAmount / MaximumAmount, 0f, HoursLeftHint);
        }
        else
        {
            this.UpdateUI(CurrentAmount / MaximumAmount, 0f, HoursLeftHint);
        }
    }

    public override void ResetToMaximum()
    {
        CurrentAmount = MaximumAmount;
        IsOnLastBar = false;
        this.UpdateUI(1f, 0f, -1);
    }

    internal Action<float, float, int> UpdateUI;
}

public class SurvivalTimer : MonoBehaviour {
    public static SurvivalTimer Instance;

    public SingleSurvivalResource Oxygen = new SingleSurvivalResource()
    {
        ConsumptionPerSecond = RedHomestead.Constants.KilogramsOxygenPerHour / 60 * SunOrbit.GameSecondsPerMartianMinute,
        MaximumAmount = RedHomestead.Constants.KilogramsOxygenPerHour * 4f,
        CurrentAmount = RedHomestead.Constants.KilogramsOxygenPerHour * 4f
    };
    public SingleSurvivalResource Water = new SingleSurvivalResource()
    {
        ConsumptionPerSecond = RedHomestead.Constants.LitersOfWaterPerDay / SunOrbit.MartianMinutesPerDay * SunOrbit.GameSecondsPerMartianMinute,
        MaximumAmount = RedHomestead.Constants.LitersOfWaterPerDay / 2,
        CurrentAmount = RedHomestead.Constants.LitersOfWaterPerDay / 2
    };
    public SingleSurvivalResource Food = new SingleSurvivalResource()
    {
        ConsumptionPerSecond = RedHomestead.Constants.CaloriesPerDay / SunOrbit.MartianMinutesPerDay * SunOrbit.GameSecondsPerMartianMinute,
        MaximumAmount = RedHomestead.Constants.CaloriesPerDay,
        CurrentAmount = RedHomestead.Constants.CaloriesPerDay
    };
    public DoubleSurvivalResource Power = new DoubleSurvivalResource()
    {
        ConsumptionPerSecond = .1f 
    };

    public bool UsingPackResources = true;

	void Awake () {
        Instance = this;
    }

    void Start()
    {
        Oxygen.UpdateUI = GuiBridge.Instance.RefreshOxygenBar;
        Water.UpdateUI = GuiBridge.Instance.RefreshWaterBar;
        Food.UpdateUI = GuiBridge.Instance.RefreshFoodBar;
        Power.UpdateUI = GuiBridge.Instance.RefreshPowerBar;
    }
	
	void Update () {
        if (UsingPackResources)
        {
            Oxygen.Consume();

            if (Oxygen.CurrentAmount < 0)
            {
                //todo: accept reason why you died: e.g. You asphyxiated
                KillPlayer();
                return;
            }

            Power.Consume();
            if (Power.IsOnLastBar && Power.CurrentAmount < 0f)
            {
                //todo: accept reason why you died: e.g. You froze
                KillPlayer();
                return;
            }
        }

        Water.Consume();

        if (Water.CurrentAmount < 0)
        {
            //todo: accept reason why you died: e.g. You terminally dehydrated
            KillPlayer();
            return;
        }

        Food.Consume();

        if (Food.CurrentAmount < 0)
        {
            //todo: accept reason why you died: e.g. You starved
            KillPlayer();
            return;
        }
    }

    private void KillPlayer()
    {
        PlayerInput.Instance.KillPlayer();
        this.enabled = false;
    }

    internal void UseHabitatResources()
    {
        Oxygen.ResetToMaximum();
        Power.ResetToMaximum();

        UsingPackResources = false;
    }

    internal void FillWater()
    {
        Water.ResetToMaximum();
    }

    internal void EatFood(MealType meal)
    {
        Food.Increment(meal.GetCalories());
    }

    internal void UsePackResources()
    {
        UsingPackResources = true;
    }
}

public enum MealType { Prepared = 0, Organic, Shake }

public static class MealTypeExtensions
{
    public static float GetCalories(this MealType meal)
    {
        switch (meal)
        {
            case MealType.Shake:
                return 600f;
            default:
                return 1200f;
        }
    }
}
