using System;
using System.Collections.Generic;
using cAlgo.API;
using cAlgo.API.Indicators;
using NLog;

namespace cAlgo
{
    class PositionController
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public Robot Robot { get; set; }

        public InputParams Params { get; set; }

        private List<Position> negativeBePositions = new List<Position>();
        private List<Position> positiveBePositions = new List<Position>();
        private List<Position> trailedPositions = new List<Position>();

        private ExponentialMovingAverage EmaHigh;

        private ExponentialMovingAverage EmaLow;

        public PositionController(Robot robot, InputParams inputParams, ExponentialMovingAverage emaHigh, ExponentialMovingAverage emaLow)
        {
            Robot = robot;
            Params = inputParams;
            EmaHigh = emaHigh;
            EmaLow = emaLow;
        }

        public void OnTick()
        {
            foreach (Position position in getPositions())
            {
                Dictionary<String, String> attributes = Utils.ParseComment(position.Comment);
                if (position.GrossProfit < 0 && !negativeBePositions.Contains(position))
                {
                   ApplyNegativeProfitStrategy(position, Params.LossStrategy);
                }             
                if (position.GrossProfit >= getProfitAmount(attributes) * Params.ProfitThreshold && Params.ProfitThreshold > 0)
                {
                    ApplyProfitStrategy(position, false);
                }
            }
        }

        public void OnBar()
        {
            foreach (Position position in getPositions())
            {
                Dictionary<String, String> attributes = Utils.ParseComment(position.Comment);
                if (position.GrossProfit >= getProfitAmount(attributes) * Params.ProfitThreshold && Params.ProfitThreshold > 0)
                {
                    ApplyProfitStrategy(position, true);
                }
            }
        }

        private List<Position> getPositions()
        {
            List<Position> filtered = new List<Position>();
            foreach (Position position in Robot.Positions)
            {
                string label = Utils.PositionLabel(Robot.SymbolName, Params.LevelFileName, Params.StrategyType.ToString());
                if (position.Label == label)
                    filtered.Add(position);
            }
            return filtered;
        }

        private void ApplyNegativeProfitStrategy(Position position, LossStrategy strategy)
        {
            if(Params.CandlesInNegativeArea > 0)
            {
                int negativeBarsCount = 0;
                for (int i = positionStartIndex(position) + 1; i < lastBarIndex() - 1; i++) // check only for finished bars
                    if (IsInNegativeArea(i, position, strategy))
                        negativeBarsCount++;

                Func<Position, double> negativeBreakOffset = p => (p.EntryPrice - p.StopLoss.Value) * Params.NegativeBreakEvenOffset;
                if (position.TradeType == TradeType.Sell)
                    negativeBreakOffset = p => (p.StopLoss.Value - p.EntryPrice) * -Params.NegativeBreakEvenOffset;

                if (negativeBarsCount == Params.CandlesInNegativeArea)
                {
                    logger.Info(String.Format("Moving Profit to Breakeven as {0}% of original Stop Loss. Reason: {1} candle(s) in negative area", Params.NegativeBreakEvenOffset, negativeBarsCount));
                    Robot.Print("Moving Profit to Breakeven as {0}% of original Stop Loss. Reason: {1} candle(s) in negative area", Params.NegativeBreakEvenOffset, negativeBarsCount);
                    double newPrice = position.EntryPrice - negativeBreakOffset(position);
                    TradeResult result = Robot.ModifyPosition(position, position.StopLoss, newPrice);
                    if (result.IsSuccessful)
                    {
                        Robot.Print(result.Error);
                        negativeBePositions.Add(position);
                    }
                    else
                    {
                        Robot.Print(result.Error.ToString());
                    }
                }
            }
        }

        private void ApplyProfitStrategy(Position position, bool onBar)
        {
            if(Params.ProfitStrategy == ProfitStrategy.SIMPLE && !onBar)
            {
                if (!positiveBePositions.Contains(position))
                    SetBreakEven(position);
                positiveBePositions.Add(position);
            }

            if (Params.ProfitStrategy == ProfitStrategy.TRAILING_ENVELOPE_50 && onBar)
            {
                int lastBar = Robot.MarketSeries.Close.Count - 1;
                double lastPrice = Robot.MarketSeries.Close.LastValue;

                if (!positiveBePositions.Contains(position))
                    SetBreakEven(position);
                positiveBePositions.Add(position);

                if (position.TakeProfit.HasValue)
                    position.ModifyTakeProfitPrice(null);

                if (position.TradeType == TradeType.Buy)
                {
                    EmaLow.Calculate(lastBar);
                    EmaHigh.Calculate(lastBar);
                    double emaLow = EmaLow.Result[lastBar];
                    double emaHigh = EmaHigh.Result[lastBar];
                    if ((lastPrice - emaHigh) / Robot.Symbol.PipSize > 5 && !trailedPositions.Contains(position))
                    {
                        position.ModifyStopLossPrice(emaLow);
                        trailedPositions.Add(position);
                    }
                    if (trailedPositions.Contains(position))
                        position.ModifyStopLossPrice(emaLow);
                }
                if (position.TradeType == TradeType.Sell)
                {
                    EmaLow.Calculate(lastBar);
                    EmaHigh.Calculate(lastBar);
                    double emaLow = EmaLow.Result[lastBar];
                    double emaHigh = EmaHigh.Result[lastBar];
                    if ((emaLow - lastPrice) / Robot.Symbol.PipSize > 5 && !trailedPositions.Contains(position))
                    {
                        position.ModifyStopLossPrice(emaHigh);
                        trailedPositions.Add(position);
                    }
                    if (trailedPositions.Contains(position))
                        position.ModifyStopLossPrice(emaHigh);
                }
            }
        }

        private void SetBreakEven(Position position)
        {
            logger.Info(String.Format("Moving Stoploss to Positive Break even. Reason: Profit is now over {0}% threshold", Params.ProfitThreshold * 100));
            Robot.Print("Moving Stoploss to Positive Break even. Reason: Profit is now over {0}% threshold", Params.ProfitThreshold * 100);
            double breakEvenPrice = position.EntryPrice + 1 * Robot.Symbol.PipSize * (position.TradeType == TradeType.Buy ? 1 : -1);
            Robot.ModifyPosition(position, breakEvenPrice, position.TakeProfit);
            if (Params.ProfitVolume > 0)
            {
                logger.Info(String.Format("Partial profit taken for {0}% of original volume of {1} units", Params.ProfitVolume, position.VolumeInUnits));
                Robot.Print("Partial profit taken for {0}% of original volume of {1} units", Params.ProfitVolume, position.VolumeInUnits);
                Robot.ModifyPosition(position, Robot.Symbol.NormalizeVolumeInUnits(position.VolumeInUnits * Params.ProfitVolume));
            }
        }

        private bool IsInNegativeArea(int index, Position position, LossStrategy strategy)
        {
            if (strategy == LossStrategy.FULL_CANDLE)
            {
                if (position.TradeType == TradeType.Buy)
                {
                    return Robot.MarketSeries.High[index] < position.EntryPrice;
                }
                return Robot.MarketSeries.Low[index] > position.EntryPrice;
            }

            if (strategy == LossStrategy.CANDLE_BODY)
            {
                if (position.TradeType == TradeType.Buy)
                    return Robot.MarketSeries.Open[index] < position.EntryPrice && Robot.MarketSeries.Close[index] < position.EntryPrice;
                return Robot.MarketSeries.Open[index] > position.EntryPrice && Robot.MarketSeries.Close[index] > position.EntryPrice;
            }
            return false;
        }

        private double getProfitAmount(Dictionary<String, String> attributes)
        {
            return Double.Parse(attributes["profit"]);
        }

        private double LastPrice(TradeType tradeType)
        {
            return tradeType == TradeType.Buy ? Robot.Symbol.Bid : Robot.Symbol.Ask;
        }

        private int positionStartIndex(Position position)
        {
            return Robot.MarketSeries.OpenTime.GetIndexByTime(position.EntryTime);
        }

        private int lastBarIndex()
        {
            return Robot.MarketSeries.High.Count;
        }
    }
}
