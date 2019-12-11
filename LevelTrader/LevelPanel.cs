using System;
using System.Collections;
using System.Collections.Generic;
using cAlgo.API;

namespace cAlgo
{
    class LevelPanel : CustomControl
    {
        List<Level> Levels;
        LevelRenderer LevelRenderer;
        Robot Robot;

        public LevelPanel(Robot robot, List<Level> levels, LevelRenderer levelRenderer)
        {
            Robot = robot;
            Levels = levels;
            LevelRenderer = levelRenderer;
            AddChild(CreateContentPanel());
        }

        public static T ParseEnum<T>(string value)
        {
            return (T)Enum.Parse(typeof(T), value, true);
        }

        public enum LevelEnabled
        {
            On,
            Off,
        }

        private ControlBase CreateContentPanel()
        {
            var contentPanel = new StackPanel
            {
                Margin = "5 5 5 5",
            };
            var grid = new Grid(Levels.Count, 3);

            int row = 0;
            foreach(Level level in Levels)
            {
                CreateRadioLabel(grid, row, level.Label, new LevelEnabled(), level.Label+"_radio", val =>
                {
                    level.Disabled = val == "Off" ? true : false;
                    Robot.Print("Level {0} {1}", level.Label, val);
                    LevelRenderer.RenderLevel(level);
                    return true;
                });
                row++;
            }
   
            contentPanel.AddChild(grid);
            return contentPanel;
        }

        private void CreateRadioLabel(Grid grid, int row, string label, Enum e, string inputKey, Func<string, bool> clickHandler)
        {
            var textBlock = new TextBlock
            {
                Text = " " +label + "  "
            };
            grid.AddChild(textBlock, row, 0);

            int idx = 0;
            foreach (string value in Enum.GetNames(e.GetType()))
            {
                var input = new RadioButton
                {
                    IsChecked = idx == 0 ? true : false,
                    Margin = "0 0 0 0",
                    Text = value,
                    GroupName = inputKey,
                    Style = Styles.CreateInputStyle()
                };
                input.Click += evt => clickHandler(value);
                grid.AddChild(input, row, idx+1);
                idx++;
            }
        }
    }

    public static class Styles
    {
        public static Style CreatePanelBackgroundStyle()
        {
            var style = new Style();
            style.Set(ControlProperty.CornerRadius, 3);
            style.Set(ControlProperty.BackgroundColor, GetColorWithOpacity(Color.FromHex("#292929"), 0.85m), ControlState.DarkTheme);
            style.Set(ControlProperty.BackgroundColor, GetColorWithOpacity(Color.FromHex("#FFFFFF"), 0.85m), ControlState.LightTheme);
            style.Set(ControlProperty.BorderColor, Color.FromHex("#3C3C3C"), ControlState.DarkTheme);
            style.Set(ControlProperty.BorderColor, Color.FromHex("#C3C3C3"), ControlState.LightTheme);
            style.Set(ControlProperty.BorderThickness, new Thickness(1));

            return style;
        }

        public static Style CreateInputStyle()
        {
            var style = new Style(DefaultStyles.TextBoxStyle);
            style.Set(ControlProperty.BackgroundColor, Color.FromHex("#1A1A1A"), ControlState.DarkTheme);
            style.Set(ControlProperty.BackgroundColor, Color.FromHex("#111111"), ControlState.DarkTheme | ControlState.Hover);
            style.Set(ControlProperty.BackgroundColor, Color.FromHex("#E7EBED"), ControlState.LightTheme);
            style.Set(ControlProperty.BackgroundColor, Color.FromHex("#D6DADC"), ControlState.LightTheme | ControlState.Hover);
            style.Set(ControlProperty.CornerRadius, 0);
            return style;
        }

        public static Style CreateCommonBorderStyle()
        {
            var style = new Style();
            style.Set(ControlProperty.BorderColor, GetColorWithOpacity(Color.FromHex("#FFFFFF"), 0.12m), ControlState.DarkTheme);
            style.Set(ControlProperty.BorderColor, GetColorWithOpacity(Color.FromHex("#000000"), 0.12m), ControlState.LightTheme);
            return style;
        }

        public static Style CreateHeaderStyle()
        {
            var style = new Style();
            style.Set(ControlProperty.ForegroundColor, GetColorWithOpacity("#FFFFFF", 0.70m), ControlState.DarkTheme);
            style.Set(ControlProperty.ForegroundColor, GetColorWithOpacity("#000000", 0.65m), ControlState.LightTheme);
            return style;
        }

        private static Color GetColorWithOpacity(Color baseColor, decimal opacity)
        {
            var alpha = (int)Math.Round(byte.MaxValue * opacity, MidpointRounding.AwayFromZero);
            return Color.FromArgb(alpha, baseColor);
        }
    }
}
