﻿using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.TimeFormatters;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace LiveSplit.UI.Components
{
    public class PreviousSegment : IComponent
    {
        protected InfoTimeComponent InternalComponent { get; set; }
        public PreviousSegmentSettings Settings { get; set; }

        protected DeltaTimeFormatter DeltaFormatter { get; set; }
        protected PossibleTimeSaveFormatter TimeSaveFormatter { get; set; }

        public float PaddingTop { get { return InternalComponent.PaddingTop; } }
        public float PaddingLeft { get { return InternalComponent.PaddingLeft; } }
        public float PaddingBottom { get { return InternalComponent.PaddingBottom; } }
        public float PaddingRight { get { return InternalComponent.PaddingRight; } }

        public IDictionary<string, Action> ContextMenuControls
        {
            get { return null; }
        }

        public PreviousSegment(LiveSplitState state)
        {
            DeltaFormatter = new DeltaTimeFormatter();
            TimeSaveFormatter = new PossibleTimeSaveFormatter();
            Settings = new PreviousSegmentSettings()
            {
                CurrentState = state
            };
            InternalComponent = new InfoTimeComponent(null, null, DeltaFormatter);
            state.ComparisonRenamed += state_ComparisonRenamed;
        }

        void state_ComparisonRenamed(object sender, EventArgs e)
        {
            var args = (RenameEventArgs)e;
            if (Settings.Comparison == args.OldName)
            {
                Settings.Comparison = args.NewName;
                ((LiveSplitState)sender).Layout.HasChanged = true;
            }
        }

        private void PrepareDraw(LiveSplitState state)
        {
            InternalComponent.DisplayTwoRows = Settings.Display2Rows;
            InternalComponent.NameLabel.HasShadow
                = InternalComponent.ValueLabel.HasShadow
                = state.LayoutSettings.DropShadows;
            InternalComponent.NameLabel.ForeColor = Settings.OverrideTextColor ? Settings.TextColor : state.LayoutSettings.TextColor;
        }

        private void DrawBackground(Graphics g, LiveSplitState state, float width, float height)
        {
            if (Settings.BackgroundColor.ToArgb() != Color.Transparent.ToArgb()
                || Settings.BackgroundGradient != GradientType.Plain
                && Settings.BackgroundColor2.ToArgb() != Color.Transparent.ToArgb())
            {
                var gradientBrush = new LinearGradientBrush(
                            new PointF(0, 0),
                            Settings.BackgroundGradient == GradientType.Horizontal
                            ? new PointF(width, 0)
                            : new PointF(0, height),
                            Settings.BackgroundColor,
                            Settings.BackgroundGradient == GradientType.Plain
                            ? Settings.BackgroundColor
                            : Settings.BackgroundColor2);
                g.FillRectangle(gradientBrush, 0, 0, width, height);
            }
        }

        public void DrawVertical(Graphics g, LiveSplitState state, float width, Region clipRegion)
        {
            DrawBackground(g, state, width, VerticalHeight);
            PrepareDraw(state);
            InternalComponent.DrawVertical(g, state, width, clipRegion);
        }

        public void DrawHorizontal(Graphics g, LiveSplitState state, float height, Region clipRegion)
        {
            DrawBackground(g, state, HorizontalWidth, height);
            PrepareDraw(state);
            InternalComponent.DrawHorizontal(g, state, height, clipRegion);
        }

        public float VerticalHeight
        {
            get { return InternalComponent.VerticalHeight; }
        }

        public float MinimumWidth
        {
            get { return InternalComponent.MinimumWidth; }
        }

        public float HorizontalWidth
        {
            get { return InternalComponent.HorizontalWidth; }
        }

        public float MinimumHeight
        {
            get { return InternalComponent.MinimumHeight; }
        }

        public string ComponentName
        {
            get { return "Previous Segment" + (Settings.Comparison == "Current Comparison" ? "" : " (" + CompositeComparisons.GetShortComparisonName(Settings.Comparison) + ")"); }
        }

        public Control GetSettingsControl(LayoutMode mode)
        {
            Settings.Mode = mode;
            return Settings;
        }

        public void SetSettings(System.Xml.XmlNode settings)
        {
            Settings.SetSettings(settings);
        }

        public System.Xml.XmlNode GetSettings(System.Xml.XmlDocument document)
        {
            return Settings.GetSettings(document);
        }

        public TimeSpan? GetPossibleTimeSave(LiveSplitState state, int splitIndex, string comparison)
        {
            var prevTime = TimeSpan.Zero;
            TimeSpan? bestSegments = state.Run[splitIndex].BestSegmentTime[state.CurrentTimingMethod];

            while (splitIndex > 0 && bestSegments != null)
            {
                var splitTime = state.Run[splitIndex - 1].Comparisons[comparison][state.CurrentTimingMethod];
                if (splitTime != null)
                {
                    prevTime = splitTime.Value;
                    break;
                }
                else
                {
                    splitIndex--;
                    bestSegments += state.Run[splitIndex].BestSegmentTime[state.CurrentTimingMethod];
                }
            }

            var time = state.Run[splitIndex].Comparisons[comparison][state.CurrentTimingMethod] - prevTime - bestSegments;

            if (time < TimeSpan.Zero)
                time = TimeSpan.Zero;

            return time;
        }

        public void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
        {
            var comparison = Settings.Comparison == "Current Comparison" ? state.CurrentComparison : Settings.Comparison;
            if (!state.Run.Comparisons.Contains(comparison))
                comparison = state.CurrentComparison;
            var comparisonName = CompositeComparisons.GetShortComparisonName(comparison);
            var componentName = "Previous Segment" + (Settings.Comparison == "Current Comparison" ? "" : " (" + comparisonName + ")");

            if (InternalComponent.InformationName != componentName)
            {
                InternalComponent.AlternateNameText.Clear();
                if (componentName.Contains("Previous Segment"))
                {
                    InternalComponent.AlternateNameText.Add("Previous Segment");
                    InternalComponent.AlternateNameText.Add("Prev. Segment");
                    InternalComponent.AlternateNameText.Add("Prev. Seg.");
                }
                else
                {
                    InternalComponent.AlternateNameText.Add("Live Segment");
                    InternalComponent.AlternateNameText.Add("Live Seg.");
                }
            }
            InternalComponent.LongestString = componentName;
            InternalComponent.InformationName = componentName;

            DeltaFormatter.Accuracy = Settings.DeltaAccuracy;
            DeltaFormatter.DropDecimals = Settings.DropDecimals;
            TimeSaveFormatter.Accuracy = Settings.TimeSaveAccuracy;

            TimeSpan? timeChange = null;
            TimeSpan? timeSave = null;
            if (state.CurrentPhase != TimerPhase.NotRunning)
            {
                bool liveSeg = false;
                if (state.CurrentPhase == TimerPhase.Running || state.CurrentPhase == TimerPhase.Paused)
                {
                    if (LiveSplitStateHelper.CheckLiveDelta(state, false, comparison, state.CurrentTimingMethod) != null)
                        liveSeg = true;
                }
                if (liveSeg)
                {
                    timeChange = LiveSplitStateHelper.GetLiveSegmentDelta(state, state.CurrentSplitIndex, comparison, state.CurrentTimingMethod);
                    timeSave = GetPossibleTimeSave(state, state.CurrentSplitIndex, comparison);
                    InternalComponent.InformationName = "Live Segment" + (Settings.Comparison == "Current Comparison" ? "" : " (" + comparisonName + ")");
                }
                else if (state.CurrentSplitIndex > 0)
                {
                    timeChange = LiveSplitStateHelper.GetPreviousSegmentDelta(state, state.CurrentSplitIndex - 1, comparison, state.CurrentTimingMethod);
                    timeSave = GetPossibleTimeSave(state, state.CurrentSplitIndex - 1, comparison);
                }
                if (timeChange != null)
                {
                    if (liveSeg)
                        InternalComponent.ValueLabel.ForeColor = LiveSplitStateHelper.GetSplitColor(state, timeChange, state.CurrentSplitIndex, false, false, comparison, state.CurrentTimingMethod).Value;
                    else
                        InternalComponent.ValueLabel.ForeColor = LiveSplitStateHelper.GetSplitColor(state, timeChange.Value, state.CurrentSplitIndex - 1, false, true, comparison, state.CurrentTimingMethod).Value;
                }
                else
                {
                    var color = LiveSplitStateHelper.GetSplitColor(state, null, state.CurrentSplitIndex - 1, true, true, comparison, state.CurrentTimingMethod);
                    if (color == null)
                        color = Settings.OverrideTextColor ? Settings.TextColor : state.LayoutSettings.TextColor;
                    InternalComponent.ValueLabel.ForeColor = color.Value;
                }
            }
            else
            {
                InternalComponent.ValueLabel.ForeColor = Settings.OverrideTextColor ? Settings.TextColor : state.LayoutSettings.TextColor;
            }
            InternalComponent.InformationValue = DeltaFormatter.Format(timeChange) 
                + (Settings.ShowPossibleTimeSave ? " / " + TimeSaveFormatter.Format(timeSave) : "");

            InternalComponent.Update(invalidator, state, width, height, mode);
        }

        public void Dispose()
        {
        }
    }
}
