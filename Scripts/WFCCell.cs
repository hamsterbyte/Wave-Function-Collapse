namespace hamsterbyte.WFC{
    using System;
    using System.Linq;

    public partial class WFCCell{
        private void PrecalculateFrequencies(){
            for (int i = 0; i < rawFrequencies.Length; i++){
                logFrequencies[i] = Math.Log2(rawFrequencies[i]);
            }

            sumOfRawFrequencies = rawFrequencies.Sum();
            sumOfPossibleFrequencies = sumOfRawFrequencies;
            for (int i = 0; i < rawFrequencies.Length; i++){
                sumOfPossibleFrequencyLogFrequencies += Math.Log2(sumOfRawFrequencies) * Math.Log2(rawFrequencies[i]);
            }
        }

        public void RemoveOption(int i){
            Options[i] = false;
            sumOfPossibleFrequencies -= rawFrequencies[i];
            sumOfPossibleFrequencyLogFrequencies -= logFrequencies[i];
        }

        public double Entropy => Math.Log2(sumOfPossibleFrequencies) - sumOfPossibleFrequencyLogFrequencies/sumOfPossibleFrequencies + entropyNoise;


        private int WeightedRandomIndex(){
            int pointer = 0;
            int randomFromSumPossible = WFCGrid.Random.Next(0, sumOfPossibleFrequencies);
            for (int i = 0; i < Options.Length; i++){
                if (!Options[i]) continue;
                pointer += rawFrequencies[i];
                if (pointer >= randomFromSumPossible){
                    return i;
                }
            }
            //If index returns -1 we know the collapse has failed.
            return -1;
        }


        public int Collapse(){
            int weightedRandomIndex = WeightedRandomIndex();
            TileIndex = weightedRandomIndex;
            Collapsed = true;
            for (int i = 0; i < Options.Length; i++){
                Options[i] = i == TileIndex;
            }
            return weightedRandomIndex;
        }
    }
}