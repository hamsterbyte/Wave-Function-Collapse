namespace hamsterbyte.WFC{
    public interface IWFCCommand{
        public void Execute();
        public void Undo();
    }

    public struct CollapseCommand : IWFCCommand{
        private WFCGrid grid;
        private WFCCell cell;
        public CollapseCommand(WFCGrid _grid, WFCCell _cell){
            grid = _grid;
            cell = _cell;
        }

        public void Execute(){
            throw new System.NotImplementedException();
        }

        public void Undo(){
            throw new System.NotImplementedException();
        }
    }
}