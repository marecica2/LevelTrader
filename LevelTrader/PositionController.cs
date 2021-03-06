﻿using System;
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

        private Calendar Calendar { get; set; }

        private List<Position> negativeBePositions = new List<Position>();
        private List<Position> positiveBePositions = new List<Position>();
        private List<Position> positivePartialProfitPositions = new List<Position>();
        private List<Position> trailedPositions = new List<Position>();

        private ExponentialMovingAverage EmaHigh;

        private ExponentialMovingAverage EmaLow;

        public PositionController(Robot robot, InputParams inputParams, ExponentialMovingAverage emaHigh, ExponentialMovingAverage emaLow, Calendar calendar)
        {
            Robot = robot;
            Params = inputParams;
            EmaHigh = emaHigh;
            EmaLow = emaLow;
            Calendar = calendar;
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

                if (position.GrossProfit > 0 && position.Pips > getProfitPips(attributes) * Params.ProfitBreakEvenThreshold && !positiveBePositions.Contains(position))
                {
                    SetBreakEven(position);
                    positiveBePositions.Add(position);
                }

                if (position.GrossProfit > 0 && position.Pips > getProfitPips(attributes) * Params.ProfitThreshold && Params.ProfitThreshold > 0 && !positivePartialProfitPositions.Contains(position))
                {
                    ApplyProfitStrategy(position, false);
                    positivePartialProfitPositions.Add(position);
                }

                if (Params.CalendarPause && Calendar.IsPaused(null))
                {
                    position.Close();
                    logger.Info(String.Format("Position closed due to calendar event"));
                    Robot.Print(String.Format("Position closed due to calendar event"));
                }

            }
        }

        public void OnBar()
        {
            foreach (Position position in getPositions())
            {
                Dictionary<String, String> attributes = Utils.ParseComment(position.Comment);
                if (position.GrossProfit > 0 && position.Pips > getProfitPips(attributes) * Params.ProfitThreshold && Params.ProfitThreshold > 0 && !positivePartialProfitPositions.Contains(position))
                {
                    ApplyProfitStrategy(position, true);
                    positivePartialProfitPositions.Add(position);
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
            if (Params.ProfitStrategy == ProfitStrategy.TRAILING_ENVELOPE_50)
            {
                int lastBar = Robot.MarketSeries.Close.Count - 1;
                double lastPrice = Robot.MarketSeries.Close.LastValue;

                if (position.TakeProfit.HasValue)
                    position.ModifyTakeProfitPrice(null);

                if (position.TradeType == TradeType.Buy && onBar)
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
                if (position.TradeType == TradeType.Sell && onBar)
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
            if (Params.ProfitStrategy == ProfitStrategy.SIMPLE)
            {
                if (Params.ProfitVolume > 0)
                {
                    logger.Info(String.Format("Partial profit taken for {0}% of original volume of {1} units", Params.ProfitVolume, position.VolumeInUnits));
                    Robot.Print("Partial profit taken for {0}% of original volume of {1} units", Params.ProfitVolume, position.VolumeInUnits);
                    Robot.ModifyPosition(position, Robot.Symbol.NormalizeVolumeInUnits(position.VolumeInUnits * Params.ProfitVolume));
                }
            }
        }

        private void SetBreakEven(Position position)
        {
            logger.Info(String.Format("Moving Stoploss to Positive Break even. Reason: Profit is now over {0}% threshold", Params.ProfitBreakEvenThreshold * 100));
            Robot.Print("Moving Stoploss to Positive Break even. Reason: Profit is now over {0}% threshold", Params.ProfitBreakEvenThreshold * 100);
            double breakEvenPrice = position.EntryPrice + 1 * Robot.Symbol.PipSize * (position.TradeType == TradeType.Buy ? 1 : -1);
            Robot.ModifyPosition(position, breakEvenPrice, position.TakeProfit);
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

        private double getProfitPips(Dictionary<String, String> attributes)
        {
            return Double.Parse(attributes["profitPips"]);
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
