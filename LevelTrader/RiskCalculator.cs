using cAlgo.API;
using cAlgo.API.Internals;

namespace cAlgo
{
    class RiskCalculator
    {
        private Robot Robot { get; set; }

        public RiskCalculator(Robot robot)
        {
            this.Robot = robot;
        }

        public double GetVolume(string symbol, double risk, double fixedRisk, double stopLossPips, TradeType tradeType)
        {
            double riskAmount = fixedRisk == 0 ? Robot.Account.Balance * risk : fixedRisk;

            double pipValue = Robot.Symbols.GetSymbol(symbol).PipValue;
            //double pipValue = CalculatePipValue(1, tradeType, Robot.Account.Currency, Robot.Symbols.GetSymbol(symbol));
            double volume = riskAmount / (pipValue * stopLossPips);
            Robot.Print("VOLUME FOR {0} with Risk: {1} SL Pips: {2}  is {3}", symbol, risk, stopLossPips, volume);
            return Robot.Symbol.NormalizeVolumeInUnits(volume);
        }

        private double CalculatePipValue(double volume, TradeType trade, string accountCurrency, Symbol pair)
        {
            string baseCurrency = pair.Name.Substring(3);
            Symbol cross = Robot.Symbols.GetSymbol(accountCurrency + baseCurrency);
            if (cross == null)
                cross = Robot.Symbols.GetSymbol(baseCurrency + accountCurrency);
            double rate = trade == TradeType.Buy ? cross.Ask : cross.Bid;
            double pip = pair.PipSize * 1 / rate;
            return pip;
        }
    }
}
