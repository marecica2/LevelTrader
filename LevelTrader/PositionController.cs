﻿using System;
using System.Collections.Generic;
using cAlgo.API;

namespace cAlgo
{
    class PositionController
    {
        public Robot Robot { get; set; }

        public InputParams Params { get; set; }

        private List<Position> modifiedPositions = new List<Position>();

        public PositionController(Robot robot, InputParams inputParams)
        {
            Robot = robot;
            Params = inputParams;
        }

        public void OnTick()
        {
            foreach (Position position in Robot.Positions)
            {
                if (position.GrossProfit < 0 && !modifiedPositions.Contains(position))
                {
                   ApplyNegativeProfitStrategy(position, Params.LossStrategy);
                }             
                if (position.GrossProfit >= getRiskAmount() * Params.ProfitThreshold)
                {
                    ApplyProfitStrategy(position);
                }
            }
        }

        private double getRiskAmount()
        {
            if (Params.FixedRiskAmount > 0)
                return Params.FixedRiskAmount;
            return Robot.Account.Balance * Params.PositionSize;
        }

        private void ApplyNegativeProfitStrategy(Position position, LossStrategy strategy)
        {
            int numOfCandles = 0;
            for (int i = positionStartIndex(position); i < lastBarIndex(); i++)
            {
                if (IsInNegativeArea(i, position, strategy))
                    numOfCandles++;
            }
            if (numOfCandles >= Params.CandlesInNegativeArea)
            {
                Robot.Print("Moving Profit to Breakeven as {0}% of original Stop Loss. Reason: {1} candle(s) in negative area", Params.NegativeBreakEvenOffset, numOfCandles);
                if (position.TradeType == TradeType.Buy)
                {
                    double negativeBreakOffset = (position.EntryPrice - position.StopLoss.Value) * Params.NegativeBreakEvenOffset;
                    TradeResult res = Robot.ModifyPosition(position, position.StopLoss, position.EntryPrice - negativeBreakOffset);
                    if(!res.IsSuccessful)
                    {
                        Robot.Print(res.Error.Value);
                    }
                }
                else
                {
                    double negativeBreakOffset = (position.StopLoss.Value - position.EntryPrice) * Params.NegativeBreakEvenOffset;
                    TradeResult res = Robot.ModifyPosition(position, position.StopLoss, position.EntryPrice + negativeBreakOffset);
                    if (!res.IsSuccessful)
                    {
                        Robot.Print(res.Error.Value);
                    }
                }
                modifiedPositions.Add(position);
            }
        }

        private void ApplyProfitStrategy(Position position)
        {
            if(!modifiedPositions.Contains(position))
            {
                Robot.Print("Moving Stoploss to Zero. Reason: Profit is now over {0}", Params.ProfitThreshold);
                Robot.ModifyPosition(position, position.EntryPrice, position.TakeProfit);
                if (Params.ProfitVolume > 0)
                {
                    Robot.ModifyPosition(position, Robot.Symbol.NormalizeVolumeInUnits(position.VolumeInUnits * Params.ProfitVolume));
                    Robot.Print("Partial profit taken for volume {0}", Params.ProfitVolume);
                }
            }
            modifiedPositions.Add(position);
        }

        private bool IsInNegativeArea(int index, Position position, LossStrategy strategy)
        {
            if (strategy == LossStrategy.FULL_CANDLE)
            {
                if (position.TradeType == TradeType.Buy)
                    return Robot.MarketSeries.High[index] < position.EntryPrice;
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