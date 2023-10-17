namespace hamsterbyte.WFC{
    using System.Diagnostics;

    public partial class WFCGrid{
        private EntropyCoordinates Observe(){
            while (!entropyHeap.IsEmpty){
                EntropyCoordinates coords = entropyHeap.Pop();
                if (!cells[coords.Coordinates.X, coords.Coordinates.Y].Collapsed) return coords;
            }
            return EntropyCoordinates.Invalid;
        }
        
        private void Collapse(Coordinates _coords){
            int collapsedIndex = cells[_coords.X, _coords.Y].Collapse();
            AnimationCoordinates.Enqueue(_coords);
            removalUpdates.Push(new RemovalUpdate(){
                Coordinates = _coords,
                TileIndex = collapsedIndex
            });
            remainingUncollapsedCells--;
        }

        private void Propagate(bool _wrap = true){
            while (removalUpdates.Count > 0){
                RemovalUpdate update = removalUpdates.Pop();
                if (update.TileIndex == -1){
                    validCollapse = false;
                    return;
                }

                Coordinates[] cardinals = Coordinates.Cardinals;
                for (int d = 0; d < adjacencyRules.GetLength(1); d++){
                    Coordinates current = cardinals[d] + update.Coordinates;
                    if (_wrap){
                        current = current.Wrap(Width, Height);
                    } else if (!IsInBounds(current)){
                        continue;
                    }

                    WFCCell currentCell = cells[current.X, current.Y];
                    if (currentCell.Collapsed) continue;
                    for (int o = 0; o < adjacencyRules.GetLength(2); o++){
                        if (adjacencyRules[update.TileIndex, d, o] == 0 && currentCell.Options[o]){
                            currentCell.RemoveOption(o);
                        }
                    }
                    entropyHeap.Push(new EntropyCoordinates(){
                        Coordinates = currentCell.Coordinates,
                        Entropy = currentCell.Entropy
                    });
                }
            }
        }
        
        public void TryCollapse(bool _wrap = true, int _maxAttempts = 100){
            Reset(true);
            Busy = true;
            Stopwatch timer = Stopwatch.StartNew();
            for (int i = 0; i < _maxAttempts; i++){
                currentAttempt++;
                WFCCell cell = cells.Random();
                entropyHeap.Push(new EntropyCoordinates(){
                    Coordinates = cell.Coordinates,
                    Entropy = cell.Entropy
                });

                while (remainingUncollapsedCells > 0){
                    EntropyCoordinates e = Observe();
                    Collapse(e.Coordinates);
                    Propagate(_wrap);
                }

                if (!validCollapse && i < _maxAttempts - 1){
                    Reset();
                }
                else{
                    break;
                }
            }
            timer.Stop();
            WFCResult result = new(){
                Grid = this,
                Success = validCollapse,
                Attempts = currentAttempt,
                ElapsedMilliseconds = timer.ElapsedMilliseconds
            };
            onComplete?.Invoke(result);
            Busy = false;
        }
    }
}
























