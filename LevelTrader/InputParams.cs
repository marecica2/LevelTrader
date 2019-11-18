namespace cAlgo
{
    public enum StrategyType
    {
        ID = 0,
        SWING,
        INVEST
    }
    public enum LossStrategy
    {
        FULL_CANDLE = 0,
        CANDLE_BODY,
    }

    public class InputParams
    {
        // Comon params
        public string Instrument { get; set; }
        public double LastPrice { get; set; }

        // Input params
        public int TimeZoneOffset { get; set; }
        public string LevelFilePath { get; set; }
        public string LevelFileName { get; set; }
        public int DailyReloadHour { get; set; }
        public int DailyReloadMinute { get; set; }

        // Risk Management
        public double PositionSize { get; set; }
        public double StopLoss { get; set; }
        public double RiskRewardRatio { get; set; }

        // Level params
        public int LevelActivate { get; set; }
        public int LevelDeactivate { get; set; }
        public double LevelOffset { get; set; }

        // Loss params
        public LossStrategy LossStrategy { get; set; }
        public int CandlesInNegativeArea { get; set; }

        // Profit params


        // Backtest
        public string BackTestPath { get; set; }
        public double NegativeBreakEvenOffset { get; set; }
        public double ProfitThreshold { get; set; }
        public double ProfitVolume { get; set; }
    }
}
