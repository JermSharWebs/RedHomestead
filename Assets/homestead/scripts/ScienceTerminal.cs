﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public struct SciTerminalDetail
{
    public RectTransform CancelButton, AcceptButton, FinishButton, MissionList;
    public Text DayText, RewardText, TitleDescription;
    public Image ClockFill;

    public void FillList(IEnumerable<IScienceExperiment> list)
    {

    }

    public void FillDetail(IScienceExperiment experiment)
    {
        TitleDescription.text = string.Format("<b>{0}</b>\n<i>{1}</i>", experiment.Title(), experiment.Experiment.Description());
        RewardText.text = string.Format("${0} REWARD", experiment.Reward);
        ExperimentStatus status = experiment.Status();
        CancelButton.gameObject.SetActive(status == ExperimentStatus.Accepted);
        AcceptButton.gameObject.SetActive(status == ExperimentStatus.Available);
        FinishButton.gameObject.SetActive(status == ExperimentStatus.Completed);
        switch (status)
        {
            case ExperimentStatus.Accepted:
                ClockFill.fillAmount = experiment.Progress;
                break;
            case ExperimentStatus.Available:
                ClockFill.fillAmount = 0f;
                break;
            case ExperimentStatus.Completed:
                ClockFill.fillAmount = 1f;
                break;
        }
        if (experiment.Progress <= 0f)
        {
            DayText.text = experiment.DurationDays + " DAY DURATION";
        }
        else
        {
            DayText.text = "Day " + experiment.Progress + " of " + experiment.DurationDays;
        }
    }
}

public class ScienceTerminal : MonoBehaviour {

    public SciTerminalDetail UI;

    internal List<BiologyScienceExperiment> AvailableBiologyMissions = new List<BiologyScienceExperiment>()
    {
        new BiologyScienceExperiment()
        {
            MissionNumber = 1,
            Progress = -1,
            Reward = 5000,
            DurationDays = 1
        }
    };
    internal List<GeologyScienceExperiment> AvailableGeologyMissions = new List<GeologyScienceExperiment>()
    {
        new GeologyScienceExperiment()
        {
            MissionNumber = 1,
            Progress = -1,
            Reward = 10000,
            DurationDays = 2
        }
    };

    // Use this for initialization
    void Start () {
        GeologyTabClick();	
	}
	
	// Update is called once per frame
	void Update () {
		
	}

    public void HoverGeology(int index)
    {
        UI.FillDetail(AvailableGeologyMissions[index]);
    }

    public void HoverBiology(int index)
    {
        UI.FillDetail(AvailableBiologyMissions[index]);
    }

    public void GeologyTabClick()
    {
        UI.FillDetail(AvailableGeologyMissions.FirstOrDefault());
        UI.FillList(AvailableGeologyMissions.Cast<IScienceExperiment>());
    }
    public void BiologyTabClick()
    {
        UI.FillDetail(AvailableBiologyMissions.FirstOrDefault());
        UI.FillList(AvailableBiologyMissions.Cast<IScienceExperiment>());
    }
}
