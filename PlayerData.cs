namespace PlantingExpert
{
    public class PlayerBombPlantCount
    {
        public PlayerBombPlantCount(int bomb = 0, bool complete = false)
        {
            _bombCount = bomb;
            _complete = complete;
        }

        private int _bombCount;
        private bool _complete;

        public int BombCount
        {
            get { return _bombCount; }
            set { _bombCount = value; }
        }

        public bool Complete
        {
            get { return _complete; }
            set { _complete = value; }
        }
    }

    public class PlayerData : PlayerBombPlantCount
    {
        public PlayerData(string achieve, string reset, int count, bool complete = true)
        {
            _timeAcheived = achieve;
            _timeReset = reset;

            BombCount = count;
            Complete = complete;
        }

        private string _timeAcheived;
        private string _timeReset;

        public string TimeAcheived
        {
            get { return _timeAcheived; }
            set { _timeAcheived = value; }
        }

        public string TimeReset
        {
            get { return _timeReset; }
            set { _timeReset = value; }
        }
    }
}
