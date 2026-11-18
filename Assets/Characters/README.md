# Иван: интеграция и устройство персонажа

Рабочий пример собран в `Assets/Scenes/SampleScene.unity`. Модель сама по себе
не содержит всю игровую систему: при переносе в другую сцену переносите настроенную
иерархию игрока, оружие, rig и камеру. Скрипты находятся в `Assets/Scripts`.
Версия редактора закреплена в `ProjectSettings/ProjectVersion.txt`.

## Компоненты и ссылки

На корне Ивана нужны `Animator` с `PlayerAnimatorController`, `CharacterController`
и `IvanMovement`. Нужна камера с тегом `MainCamera` и `ShoulderCamera`, у которой
`target` указывает на игрока. Используется глобальный `InputSystem.actions`.

`IvanMovement.Awake()` добавляет отсутствующие `CombatHealth` и `PistolCombat`.
`CombatHealth` добавляет `ProceduralHitReaction`; `PistolCombat.Start()` добавляет
`PistolShotAfterPose`. Чтобы менять их параметры в Inspector до Play, добавьте эти
компоненты вручную на тот же корень: дубликаты автоматически не создаются.

Дополнительно настроены:

- `PistolVisibility`: `weaponRoot` — дочерний Glock. Меняет только Renderer.enabled.
- `PistolAimRig`: ссылки на `weaponRig`, `aimTarget`, `weaponAimTarget`, `aimCamera`.
  Две цели должны быть разными объектами.
- `RigBuilder` и WeaponRig: порядок ограничений **BodyAim → RightArmAim → LeftHandIK**.
- `PistolWeaponAimConstraint`: правая плечевая кость, дочернее дуло и цель вне этой руки.
- `PistolCombat`: `muzzle` и `aimCamera`. Если не назначены, ищет дочерний Transform
  с точным именем `Muzzle` и ShoulderCamera на MainCamera. При нескольких камерах
  или нескольких объектах Muzzle назначайте ссылки явно.

## Управление

| Действие | Источник |
|---|---|
| Ходьба | `Player/Move` в Input Actions |
| Спринт | `Player/Sprint` |
| Приседание, переключатель C | `Player/Crouch` |
| Прицеливание | удержание ПКМ, Mouse.current |
| Один выстрел | нажатие ЛКМ, Mouse.current |
| Перезарядка при прицеливании | R, Keyboard.current |
| Освободить курсор | Escape |
| Захватить курсор | ЛКМ |

Стрельба, камера и R пока читают устройства напрямую, а не переназначаемые Input
Actions. Потеря фокуса снимает прицеливание и освобождает курсор. На паузе выстрелы
и переключение приседа не выполняются. Из приседа нельзя встать под препятствием;
попытка прицеливания не обходит эту проверку.

## Публичный API

Свойства ниже доступны для чтения; их setters закрыты. Не меняйте backing fields
и приватные флаги через reflection в игровом коде.

| Компонент | API | Назначение |
|---|---|---|
| IvanMovement | `IsAiming`, `IsCrouching` | Текущее логическое состояние |
| IvanMovement | `StopForDeath()` | Останавливает управление, сохраняя гравитацию; не наносит урон |
| CombatHealth | `HP`, `MaximumHP`, `IsDead` | Здоровье и смерть |
| CombatHealth | `TakeDamage(amount, point, direction, source)` | Единственная точка нанесения урона |
| CombatHealth | `Changed: Action<float>` | Новое HP после принятого урона |
| CombatHealth | `onHit`, `onDeath`: UnityEvent | Подключение звука, AI и другой логики |
| PistolCombat | `Ammo`, `ReserveAmmo`, `IsReloading` | Магазин, запас, перезарядка |
| PistolCombat | `CancelReload()` | Отменить без переноса патронов |
| ShoulderCamera | `Yaw`, `YawDelta` | Угол и изменение угла камеры |
| ShoulderCamera | `GetAimRay()` | Луч прицеливания; кэшируется в пределах кадра |
| ShoulderCamera | `AddRecoil(degrees)` | Вертикальный толчок камеры |
| PistolAimRig | `AimPoint` | Точка, рассчитанная прицельным rig |
| ProceduralHitReaction | `Kick()`, `StopReaction()` | Только визуальная реакция, без изменения HP |

`EvaluateShotAfterPose()` публичен для вспомогательного компонента; не вызывайте
его дополнительно из AI/UI. Это обработка ввода, а не универсальный метод Fire.
Публичных методов лечения, выдачи патронов, возрождения или программного переключения
приседа пока нет. Для этих функций нужно расширить API, а не обходить инварианты.

Пример урона Ивану из ловушки или NPC:

```csharp
CombatHealth health = player.GetComponent<CombatHealth>();
health.TakeDamage(25f, impactPoint, attackDirection.normalized, attacker);
```

Для UI подпишитесь на `Changed` после инициализации, сначала прочитайте `HP`, а при
отключении UI отпишитесь. Событие не отправляет начальное значение автоматически.
`Changed` вызывается перед обработкой реакции/смерти; `onHit` вызывается также при
смертельном попадании. `onDeath` — один раз. Не вызывайте урон рекурсивно из этих событий.
Нулевой, отрицательный, NaN/Infinity урон и урон уже мёртвому объекту игнорируются.

## NPC и объекты, получающие урон

Оружие ищет первый `IDamageable` среди MonoBehaviour на коллайдере и его родителях:

```csharp
public interface IDamageable
{
    void TakeDamage(float amount, Vector3 point, Vector3 direction, GameObject source);
}
```

Можно добавить `CombatHealth` на корень NPC либо реализовать интерфейс в собственном
компоненте. Не ставьте два получателя урона на одну цепочку родителей. Коллайдеры
тела должны быть не-trigger и входить в `hitLayers`. Собственные коллайдеры игрока
исключаются по принадлежности его иерархии.

`CombatHealth` принимает point/direction/source, но пока не использует их для выбора
стороны реакции: корпус всегда отклоняется назад и влево. Для агрессии NPC или
направленных реакций реализуйте обработку этих аргументов. NPC без ИванMovement
должен останавливать AI самостоятельно через `onDeath`.

## Настройки Inspector и значения по умолчанию

| Компонент | Поля |
|---|---|
| IvanMovement | `forwardSpeed=2`, `runMultiplier=2`, `acceleration=8`, `rotationSpeed=540`, `aimRotationSpeed=540`, `animationDampTime=0.1`, `idleTurnThreshold=35`, `crouchHeight=1.1` |
| PistolCombat | `damage=25`, `range=100`, `shotInterval=0.18`, `magazineCapacity=8`, `startingTotalAmmo=24`, `reloadDuration=2` |
| Отдача PistolCombat | `recoilDegrees=1.5`, `armKickDegrees=5`, `armReturnTime=0.12` |
| CombatHealth | `maximumHP=100` |
| ProceduralHitReaction | `backwardDegrees=7`, `leftDegrees=5`, `duration=0.22`; `chest` автоматически берётся из Humanoid Chest/Spine |
| ShoulderCamera | `offset`, `aimOffset`, `mouseSensitivity`, `pitchLimits`, `collisionRadius`, `aimFieldOfView`, `aimBlendTime` |
| PistolAimRig | `aimDistance`, `minimumAimDistance`, `aimLayers`, `bodyAimHeight`, `bodyPitchLimits`, `bodyPitchSpeed`, `blendInTime`, `blendOutTime` |
| PistolVisibility | `showOnDraw=0.25`, `hideOnHolster=0.75` |

Сериализованные значения сцены имеют приоритет над начальными значениями C#.
`startingTotalAmmo` включает магазин: стартовые 24 = 8 в магазине + 16 в запасе.
Перезарядка переносит только недостающие патроны и не создаёт новые. Попадание,
смерть, отключение combat или выход из прицеливания отменяют её. Таймер использует
игровое время. Клип должен быть согласован с `reloadDuration`; Animation Events
для выдачи патронов и нанесения урона не нужны.

## Animator: контракт с кодом

| Параметр | Тип |
|---|---|
| MoveX, MoveZ | Float |
| AimHeld, IsCrouch, IsReload | Bool |
| Die | Trigger |
| Fire | Необязательный Trigger; отдельный клип выстрела не требуется |

Важные точные имена Base Layer: `Locomotion`, `PistolUp`, `PistolDown`,
`PistolLocomotion`, `Turn Left`, `Turn Right`, `Turn Back`. Для разворотов код также
ищет клипы с именами Turn Left/Right/Back. Переименование требует изменений C#.
Текущая смерть — состояние `die` в **Base Layer**, вход по Trigger `Die`.
При смерти код обнуляет веса всех слоёв выше базового и выключает прицельный rig.
Отключать ИванMovement для смерти не нужно: StopForDeath сохраняет падение на землю.

Перезарядка находится в `ReloadUpperBody` (Override, вес 1) с маской верхней части
тела. Тег состояния **Reload** блокирует стрельбу/прицельный rig, но оставляет ноги
в движении. Тег **CombatBlock** дополнительно блокирует движение и поворот: используйте
его для смерти, не для перезарядки. Проверяются также входящие переходы состояний.
Реакции HitStanding/HitArmed/HitCrouch больше не используются.

## Внутренний порядок кадра

1. ShoulderCamera (`-100`) читает мышь.
2. IvanMovement (`-80`) обрабатывает ввод, поворот, капсулу, гравитацию и MoveX/MoveZ.
3. PistolAimRig (`-50`) обновляет цели и вес rig.
4. PistolCombat (`-40`) обновляет таймер перезарядки и величину отдачи для rig.
5. Animator/Animation Rigging вычисляют позу: BodyAim, правая рука, затем левая IK.
6. В LateUpdate камера занимает рассчитанное положение; ProceduralHitReaction (`200`)
   добавляет отклонение корпуса. В начале следующего кадра оно снимается.
7. PistolShotAfterPose (`300`) читает окончательное положение дула и вызывает обработку
   выстрела. Не переносите её раньше процедурной реакции.

Стрельба hitscan: камера выбирает цель, затем луч от дула проверяет фактическое попадание.
Overlap у дула и луч от центра капсулы к дулу отклоняют выстрел через близкую стену.
Только после этой проверки расходуется патрон. Трассер — один кадр, попадание —
краткое кольцо. Руки получают процедурную отдачу через animation job, камера — отдельно.
Это не физический снаряд: скорости полёта, пробития и баллистического падения пока нет.

Присед сохраняет нижнюю точку капсулы; перед вставанием проверяется место для полной
высоты. Скорость приседа пока равна обычной ходьбе. Камера не опускается вместе с приседом.
Apply Root Motion отключён: положение игрового объекта задаёт CharacterController.
Вертикальное падение тела в клипе смерти должно оставаться в позе (Bake Into Pose Y).

## Проверки и ограничения

Тесты: `Assets/Tests/EditMode` и `Assets/Tests/PlayMode`. Текущий набор: 4 EditMode и
3 PlayMode (загрузка сцены, восстановление разворота, регрессии combat/приседа).
Регрессии проверяют частичную/отменённую перезарядку, однократную смерть, потолок,
пересечение дула и неизменность FOV отключённой камеры. Это не заменяет визуальную
проверку маски, ног, отдачи и высоты тела в Play Mode.

HUD пока OnGUI. Для сборок явно назначьте `effectMaterial`: fallback Shader.Find
может не сохранить нужный shader при stripping. Пулы эффектов, сетевой режим,
сохранение состояния и respawn не реализованы. Для respawn создавайте новый экземпляр
настроенного игрока, а не просто включайте обратно мёртвый объект.
