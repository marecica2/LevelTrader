using cAlgo.API;

namespace cAlgo
{
    class RiskCalculator
    {
        private Robot Robot { get; set; }

        public RiskCalculator(Robot robot)
        {
            this.Robot = robot;
        }

        public double GetVolume(string symbol, double risk, double stopLoss, TradeType tradeType)
        {
            double volume;
            // Robot.Print(symbol + " SL PIP " + stopLoss + " pip value " + Robot.Symbol.TickValue);

            Rates rate =  getRate(symbol);
            switch (rate)
            {
                case Rates.Direct:
                    volume = Robot.Account.Equity * (risk / 100) / (stopLoss * Robot.Symbol.PipValue);
                    break;
                case Rates.Indirect:
                    double stopLossPrice = tradeType == TradeType.Buy
                        ? Robot.Symbol.Ask + stopLoss * Robot.Symbol.PipSize
                        : Robot.Symbol.Bid - stopLoss * Robot.Symbol.PipSize;
                    volume = Robot.Account.Equity * (risk / 100) * stopLossPrice / (stopLoss * Robot.Symbol.PipValue);
                    break;
                default:
                    volume = 0;
                    break;
            }
            if (symbol.Contains("JPY"))
                volume = volume * 0.01;
            // Robot.Print(volume);
            return (double) Robot.Symbol.NormalizeVolumeInUnits(volume);
        }

        private Rates getRate(string symbol)
        {
            var rate = Rates.Cross;
            switch (symbol)
            {
                case "EURUSD":
                case "GBPUSD":
                case "AUDUSD":
                case "NZDUSD":
                    rate = Rates.Direct;
                    break;
                case "USDJPY":
                case "USDCHF":
                case "USDCAD":
                    rate = Rates.Indirect;
                    break;
                default:
                    rate = Rates.Cross;
                    break;
            }
            return rate;
        }
    }

    enum Rates
    {
        Direct,
        Indirect,
        Cross
    }
}
