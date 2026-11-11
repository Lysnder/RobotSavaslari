namespace RobotSavaslari.Robots
{
    /// <summary>
    /// Hareket bileşenine gönderilen tek bir komut: ileri/geri ve dönüş.
    /// Her iki eksen de -1 ile 1 arasındadır; klavye, yapay zekâ ya da ağ
    /// katmanı aynı komutu üretebilir.
    /// </summary>
    public readonly struct RobotMoveCommand
    {
        /// <summary>1 = tam ileri, -1 = tam geri, 0 = gaz yok.</summary>
        public readonly float Forward;

        /// <summary>1 = sağa dön, -1 = sola dön, 0 = dönüş yok.</summary>
        public readonly float Turn;

        public RobotMoveCommand(float forward, float turn)
        {
            Forward = forward;
            Turn = turn;
        }

        /// <summary>Komut bırakıldığında gönderilen boş komut.</summary>
        public static RobotMoveCommand Idle => new RobotMoveCommand(0f, 0f);

        public override string ToString() => $"RobotMoveCommand(forward: {Forward:0.##}, turn: {Turn:0.##})";
    }
}
