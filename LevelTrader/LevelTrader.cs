using System;
using cAlgo.API.Indicators;
using cAlgo.Indicators;
using cAlgo.API;
using cAlgo.API.Internals;

using NLog;
using NLog.Targets;
using NLog.Config;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.FullAccess)]
    public class LevelTrader : Robot
    {
        [Parameter("Strategy", DefaultValue = 0, Group = "Input")]
        public StrategyType StrategyType { get; set; }

        [Parameter("Folder", DefaultValue = "C:\\Users\\marec\\Documents\\NinjaTrader 8\\bin\\MarketProfitPack\\CustomLevels\\", Group = "Input")]
        public string FilePath { get; set; }

        [Parameter("File Name", DefaultValue = "FE_NT8.xml", Group = "Input")]
        public string FileName { get; set; }


        [Parameter("Time Zone Offset to UTC [Hrs]", DefaultValue = -1, MinValue = -12, MaxValue = 12, Group = "Input")]
        public double TimeZoneOffset { get; set; }

        [Parameter("Daily Update Time [UTC]", DefaultValue = "07:00", Group = "Input")]
        public string DailyReloadTime { get; set; }

        [Parameter("Level ID", Group = "Input")]
        public string LevelId { get; set; }



        [Parameter("Position Size [%]", DefaultValue = 1, MinValue = 0.01, MaxValue = 10, Group = "Risk Management", Step = 0.5)]
        public double PositionSizePercents { get; set; }

        [Parameter("Fixed Risk [Currency]", DefaultValue = 0, MinValue = 0, Group = "Risk Management", Step = 50.0)]
        public double FixedRisk { get; set; }

        [Parameter("Max Spread [Pips]", DefaultValue = 1, MinValue = 0, Group = "Risk Management", Step = 0.1)]
        public double MaxSpread { get; set; }

        [Parameter("Free Margin Minimum [%]", DefaultValue = 1, MinValue = 0, MaxValue = 1, Group = "Risk Management", Step = 0.1)]
        public double MarginThreshold { get; set; }



        [Parameter("Activate Level Distance [%]", DefaultValue = 30, MinValue = 0, MaxValue = 100, Group = "Level Control")]
        public int ActivateLevelPercents { get; set; }

        [Parameter("Deactivate Level Distance [%]", DefaultValue = 90, MinValue = 0, MaxValue = 100, Group = "Level Control", Step = 1.0)]
        public int DeactivateLevelPercents { get; set; }

        [Parameter("Level Offset [Ticks]", DefaultValue = 0, MinValue = -100, MaxValue = 100, Group = "Level Control", Step = 1.0)]
        public double LevelOffset { get; set; }



        [Parameter("Loss Strategy", DefaultValue = 0, MinValue = 0, MaxValue = 1, Group = "Loss Control")]
        public LossStrategy LossStrategy { get; set; }

        [Parameter("Default Stop Loss [Pips]", DefaultValue = 8, MinValue = 6, Group = "Loss Control")]
        public int DefaultStopLossPips { get; set; }

        [Parameter("Use ATR based SL", DefaultValue = false, Group = "Loss Control")]
        public bool UseAtrBasedStoppLossPips { get; set; }

        [Parameter("Prevent Spikes", DefaultValue = true, Group = "Loss Control")]
        public bool PreventSpikes { get; set; }

        [Parameter("Number of Candles in Negative Area", DefaultValue = 3, MinValue = 0, Group = "Loss Control")]
        public int CandlesInNegativeArea { get; set; }

        [Parameter("Negative BE Offset [% of SL]", DefaultValue = 10, MinValue = -100, MaxValue = 100, Group = "Loss Control", Step = 1.0)]
        public double NegativeBreakEvenOffset { get; set; }



        [Parameter("Risk Reward Ratio [%]", DefaultValue = 1, MinValue = 0.5, Group = "Profit Control", Step = 0.1)]
        public double RiskRewardRatio { get; set; }

        [Parameter("Positive Break Even Level[% of PT]", DefaultValue = 50, MinValue = 0, MaxValue = 100, Group = "Profit Control", Step = 5.0)]
        public double ProfitBreakEvenThreshold { get; set; }

        [Parameter("Partial Profit Level[% of PT]", DefaultValue = 60, MinValue = 0, MaxValue = 95, Group = "Profit Control", Step = 5.0)]
        public double ProfitThreshold { get; set; }

        [Parameter("Partial Profit Volume[% of PT]", DefaultValue = 50, MinValue = 0, MaxValue = 100, Group = "Profit Control", Step = 5.0)]
        public double ProfitVolume { get; set; }

        [Parameter("Profit Strategy", DefaultValue = 0, Group = "Profit Control")]
        public ProfitStrategy ProfitStrategy { get; set; }



        [Parameter("Pause Trading on Calendar Events", DefaultValue = true, Group = "Event Calendar")]
        public bool CalendarPause { get; set; }

        [Parameter("Offset Before Event [min]", DefaultValue = 20, MinValue = 0, MaxValue = 9999, Group = "Event Calendar")]
        public int CalendarBeforeOffset { get; set; }



        [Parameter("Backtest Folder", DefaultValue = "C:\\Users\\marec\\Documents\\TRADING_BACKTEST", Group = "Backtest")]
        public string BackTestPath { get; set; }

        [Parameter("Email", Group = "Notification")]
        public string Email { get; set; }

        private LevelController LevelController;

        private Calendar Calendar;

        private PositionController PositionController;

        private InputParams InputParams;

        private AverageTrueRange Atr;

        private ExponentialMovingAverage EmaHigh;

        private ExponentialMovingAverage EmaLow;

        protected override void OnStart()
        {
            InitLogger();
            Timer.Start(60);

            InputParams = new InputParams 
            {
                StrategyType = StrategyType,
                Instrument = Symbol.Name,
                LastPrice = Symbol.Bid,
                LevelFilePath = FilePath,
                LevelFileName = FileName,
                DailyReloadHour = int.Parse(DailyReloadTime.Split(new string[] 
                {
                    ":"
                }, StringSplitOptions.None)[0]),
                DailyReloadMinute = int.Parse(DailyReloadTime.Split(new string[] 
                {
                    ":"
                }, StringSplitOptions.None)[1]),
                TimeZoneOffset = TimeZoneOffset,
                LevelId = LevelId,

                PositionSize = PositionSizePercents * 0.01,
                FixedRiskAmount = FixedRisk,
                MaxSpread = MaxSpread,
                MarginTreshold = MarginThreshold,

                LevelActivate = ActivateLevelPercents * 0.01,
                LevelDeactivate = DeactivateLevelPercents * 0.01,
                LevelOffset = LevelOffset,

                LossStrategy = LossStrategy,
                DefaultStopLossPips = DefaultStopLossPips,
                UseAtrBasedStoppLossPips = UseAtrBasedStoppLossPips,
                PreventSpikes = PreventSpikes,
                CandlesInNegativeArea = CandlesInNegativeArea,
                NegativeBreakEvenOffset = NegativeBreakEvenOffset * 0.01,

                ProfitBreakEvenThreshold = ProfitBreakEvenThreshold * 0.01,
                RiskRewardRatio = RiskRewardRatio * 0.01,
                ProfitThreshold = ProfitThreshold * 0.01,
                ProfitVolume = ProfitVolume * 0.01,
                ProfitStrategy = ProfitStrategy,

                CalendarPause = CalendarPause,
                CalendarEventDuration = CalendarBeforeOffset,

                BackTestPath = BackTestPath,
                Email = Email
            };

            MarketSeries daily = MarketData.GetSeries(TimeFrame.Daily);
            Atr = Indicators.AverageTrueRange(daily, 70, MovingAverageType.Simple);
            EmaHigh = Indicators.ExponentialMovingAverage(MarketSeries.High, 50);
            EmaLow = Indicators.ExponentialMovingAverage(MarketSeries.Low, 50);

            Calendar = new Calendar(this, InputParams);
            Calendar.Init();
            LevelController = new LevelController(this, InputParams, Calendar);
            double atrPips = Math.Round(Atr.Result[Atr.Result.Count - 1] / Symbol.PipSize);
            if (RunningMode != RunningMode.RealTime)
                LevelController.Init(atrPips, Server.TimeInUtc.AddDays(-1));
            else
                LevelController.Init(atrPips);
            
            
            PositionController = new PositionController(this, InputParams, EmaHigh, EmaLow, Calendar);
            Print("LevelTrader version 2.0 started");
        }

        protected override void OnTick()
        {
            LevelController.OnTick();
            PositionController.OnTick();
        }

        protected override void OnBar()
        {
            PositionController.OnBar();
        }

        protected override void OnTimer()
        {
            Calendar.OnMinute();
            double atrPips = Math.Round(Atr.Result[Atr.Result.Count - 1] / Symbol.PipSize);
            LevelController.OnMinute(atrPips);
        }

        protected override void OnStop()
        {

        }

        protected void InitLogger()
        {
            var config = new LoggingConfiguration();
            var fileTarget = new FileTarget("target2")
            {
                ArchiveNumbering = ArchiveNumberingMode.DateAndSequence,
                ArchiveAboveSize = 10485760,
                FileName = BackTestPath + "/logs/" + this.SymbolName + "_" + this.FileName + (this.RunningMode == RunningMode.RealTime ? "" : "_backtest") + ".txt",
                Layout = "${longdate} ${callsite:className=True:fileName=False:includeSourcePath=False:methodName=False} ${level} ${message} ${exception:format=ToString,StackTrace} ${stacktrace:format=DetailedFlat:topFrames=5:skipFrames=5:separator=&#13;&#10;}"
            };
            config.AddTarget(fileTarget);
            config.AddRuleForOneLevel(LogLevel.Debug, fileTarget);
            config.AddRuleForOneLevel(LogLevel.Info, fileTarget);
            config.AddRuleForOneLevel(LogLevel.Warn, fileTarget);
            config.AddRuleForOneLevel(LogLevel.Error, fileTarget);
            LogManager.Configuration = config;
        }
    }
}
