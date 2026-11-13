namespace RobotSavaslari.Driving
{
    /// <summary>
    /// Yeniden eşlenebilen sürüş eylemleri. Kamera, menü ve silah tuşları bu
    /// listede yoktur; onlar kendi sahiplerinde kalır.
    /// </summary>
    public enum DriveAction
    {
        Forward,
        Backward,
        TurnLeft,
        TurnRight,
        Boost,
        Handbrake,
    }
}
