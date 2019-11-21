using System;
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
            int negativeBarsCount = 0;
            for (int i = positionStartIndex(position) + 1; i < lastBarIndex() - 1; i++) // check only for finished bars
                if (IsInNegativeArea(i, position, strategy))
                    negativeBarsCount++;

            Func<Position, double> negativeBreakOffset = p => (p.EntryPrice - p.StopLoss.Value) * Params.NegativeBreakEvenOffset;
            if (position.TradeType == TradeType.Sell)
                negativeBreakOffset = p => (p.StopLoss.Value - p.EntryPrice) * -Params.NegativeBreakEvenOffset;

            if (negativeBarsCount == Params.CandlesInNegativeArea)
            {
                Robot.Print("Moving Profit to Breakeven as {0}% of original Stop Loss. Reason: {1} candle(s) in negative area", Params.NegativeBreakEvenOffset, negativeBarsCount);
                double newPrice = position.EntryPrice - negativeBreakOffset(position);
                TradeResult result = Robot.ModifyPosition(position, position.StopLoss, newPrice);
                if(result.IsSuccessful)
                {
                    Robot.Print(result.Error);
                    modifiedPositions.Add(position);
                } else
                {
                    Robot.Print(result.Error.ToString());
                }
            }
        }

        private double LastPrice(TradeType tradeType)
        {
            return tradeType == TradeType.Buy ? Robot.Symbol.Bid : Robot.Symbol.Ask;
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
