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

        public double GetRisk(double risk, double fixedRisk)
        {
            return fixedRisk == 0 ? Robot.Account.Balance * risk : fixedRisk;
        }

        public double GetVolume(string symbol, double risk, double fixedRisk, double stopLossPips, TradeType tradeType)
        {
            double pipValue = Robot.Symbols.GetSymbol(symbol).PipValue;
            //double pipValue = CalculatePipValue(1, tradeType, Robot.Account.Currency, Robot.Symbols.GetSymbol(symbol));

            double riskAmount = GetRisk(risk, fixedRisk);
            double volume = riskAmount / (pipValue * stopLossPips);

            double lots = volume / 100000;
            double fee = lots * 3.5 * 2;

            riskAmount += fee;
            volume = riskAmount / (pipValue * stopLossPips);

            // Robot.Print("Volume for {0} with Risk: {1} SL Pips: {2}  is {3}", symbol, risk, stopLossPips, volume);
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
