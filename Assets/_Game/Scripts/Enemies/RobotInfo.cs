using Stealth.Level;

namespace Stealth.Enemies
{
    /// <summary>What players learn by looking at a robot: its name and how it senses (shown under the crosshair).</summary>
    public static class RobotInfo
    {
        public static string Name(RobotKind k) => k switch
        {
            RobotKind.Guard => "Охранник",
            RobotKind.Listener => "Слухач",
            RobotKind.Drone => "Дрон",
            RobotKind.Spider => "Паук",
            RobotKind.Vacuum => "Пылесос",
            RobotKind.Turret => "Турель",
            RobotKind.Thumper => "Сейсмик",
            RobotKind.Hound => "Ищейка",
            RobotKind.Sorter => "Сортировщик",
            RobotKind.Worker => "Рабочий",
            RobotKind.Hunter => "Охотник",
            RobotKind.Lidar => "Лидар",
            RobotKind.MotionSensor => "Датчик движения",
            RobotKind.PressurePlate => "Нажимная плита",
            RobotKind.Camera => "Камера",
            _ => "Робот",
        };

        public static string Senses(RobotKind k) => k switch
        {
            RobotKind.Guard => "глаза · слабый слух · ходит по лестницам",
            RobotKind.Listener => "слепой · микрофоны · колёса, свой этаж",
            RobotKind.Drone => "камера сверху · тревога · летает",
            RobotKind.Spider => "глаза · прыгает со стены · не покидает комнату",
            RobotKind.Vacuum => "линия сканера на полу · тревога · перепрыгни",
            RobotKind.Turret => "глаза · стреляет после прицеливания",
            RobotKind.Thumper => "чувствует шаги через пол · присядь или замри",
            RobotKind.Hound => "идёт по запаху · ходит по лестницам",
            RobotKind.Sorter => "видит только лут своего цвета",
            RobotKind.Worker => "не дерётся · доносит (тревога)",
            RobotKind.Hunter => "все датчики · быстрый",
            RobotKind.Lidar => "луч у пола · перепрыгни",
            RobotKind.MotionSensor => "замечает быстрое движение · крадись",
            RobotKind.PressurePlate => "не наступай · перепрыгни",
            RobotKind.Camera => "обзор · поднимает тревогу",
            _ => "",
        };
    }
}
