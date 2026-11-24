# Звуки предметов и окружения

Из локальной папки `/Users/tesler/Documents/arx_files/sfx` для этих событий отобраны 53 исходных WAV из 883. В проекте 54 обработанных клипа. Из одной записи каменной двери сделаны короткий подъём гнезда и длинный ход стены.

Уместность оценивалась по материалу объекта, типу действия, длительности движения, атаке и затуханию записи. Исходники декодированы полностью. Игровые копии — моно PCM WAV, 16 бит, на канале «Эффекты». Координаты источника и расстояние учитываются для действий в мире. Звуки журнала, сумки и перехода через дверь воспроизводятся у слушателя.

## Комната 7.2 и механизмы архива

| Исходный WAV | Игровой клип | Где звучит | Почему подходит |
| --- | --- | --- | --- |
| `puzzle_wheel.wav` | [RuneDial](../../Assets/Resources/GameAudio/RuneDial.wav) | 7.2, поворот каменного диска | Точное совпадение. Сохранили атаку и подогнали ход механизма под поворот за 0,72 с. |
| `door_stone_open.wav` | [StoneLift](../../Assets/Resources/GameAudio/StoneLift.wav) | 7.2, подъём шести каменных гнёзд | Адаптация. Короткая фаза трения соответствует небольшому ходу. Громкость каждого гнезда уменьшена в шесть раз. |
| `stone_on_stone_1.wav` | [Stone](../../Assets/Resources/GameAudio/Stone.wav) | Поднять руну, посадить её в гнездо, упор каменного механизма, удар рукоятью пистолета по активному кристаллу | Точное совпадение. Короткий контакт камня с камнем привязан к моменту касания. |
| `gravel_on_stone_1.wav` | [StoneTile](../../Assets/Resources/GameAudio/StoneTile.wav) | Плитка пятнашек и извлечение руны | Адаптация. Негромкий шорох крошки отличает скольжение от удара при посадке. |
| `door_stone_open.wav` | [StoneWall](../../Assets/Resources/GameAudio/StoneWall.wav) | Сдвиг каменной стены и заслонки М3 | Точное совпадение материала. Длинный скрежет передаёт массу кладки. |
| `porticulis_twist.wav` | [Crank](../../Assets/Resources/GameAudio/Crank.wav) | М0, поворот каждой из двух рукоятей | Точное совпадение. Слышен короткий ход привода решётки. |
| `porticullis_open.wav` | [Gate](../../Assets/Resources/GameAudio/Gate.wav) | М0 и другие подъёмные железные решётки | Точное совпадение. Движение металла сжато по времени, упор привязан отдельно. |
| `porticullis_close.wav` | [GateClose](../../Assets/Resources/GameAudio/GateClose.wav) | Опускание железной решётки | Точное совпадение. У закрытия другой характер движения. |
| `metal_on_metal_1.wav` | [GateStop](../../Assets/Resources/GameAudio/GateStop.wav) | Железная решётка достигла упора | Точное совпадение. Короткий контакт металла без длинного гула. |
| `lever.wav` | [Lever](../../Assets/Resources/GameAudio/Lever.wav) | Настенный рычаг развилки Мew | Точное совпадение. Механический контакт соответствует движению рукояти. |
| `dresser_chest_open.wav` | [Slide](../../Assets/Resources/GameAudio/Slide.wav) | Выдвижная деревянная крышка ниши D01 | Адаптация. Короткое движение деревянной мебели подходит масштабу крышки. |
| `secret_button.wav` | [Switch](../../Assets/Resources/GameAudio/Switch.wav) | Нажать скрытую кнопку или механическую печать | Точное совпадение. Механический ход и щелчок слышны в точке нажатия. |
| `door_locked.wav` | [Locked](../../Assets/Resources/GameAudio/Locked.wav) | Попытка открыть запертую дверь или замок | Точное совпадение. Короткая дрожь защёлки сообщает об отказе. |
| `door_unlock.wav` | [Unlock](../../Assets/Resources/GameAudio/Unlock.wav) | Открыть М3 ключом и отпереть нижнюю дверь церкви | Точное совпадение. Поворот ключа отличается от звона связки при подборе. |

## Церковь, двери, предметы и журнал

| Исходный WAV | Игровой клип | Где звучит | Почему подходит |
| --- | --- | --- | --- |
| `drag.wav` | [CabinetMove](../../Assets/Resources/GameAudio/CabinetMove.wav) | Церковь, отодвинуть шкаф у тайного хода | Адаптация. Взяты 0,85 с волочения под длительность движения шкафа. |
| `door_wood_open.wav` | [Door](../../Assets/Resources/GameAudio/Door.wav) | Открытие распашной деревянной двери и вход в церковь | Точное совпадение. Слышны петля и начало хода створки. |
| `door_wood_close.wav` | [DoorClose](../../Assets/Resources/GameAudio/DoorClose.wav) | Закрытие распашной двери | Точное совпадение. Удар притвора отличается от открытия. |
| `key_ring.wav` | [Key](../../Assets/Resources/GameAudio/Key.wav) | Подбор ключей и установка рукоятей М0 | Точное совпадение для ключей, близкое для съёмных металлических рукоятей. |
| `interface_backpack.wav` | [InventoryBag](../../Assets/Resources/GameAudio/InventoryBag.wav) | Открытие и закрытие инвентаря Ивана | Точное совпадение. Движение сумки обозначает переход в инвентарь. |
| `book_open.wav` | [BookOpen](../../Assets/Resources/GameAudio/BookOpen.wav) | Открытие архивного журнала | Точное совпадение. Подняли уровень тихой записи обложки. |
| `book_page_turn.wav` | [Paper](../../Assets/Resources/GameAudio/Paper.wav) | Бумажные документы, расчёт и перелистывание журнала | Точное совпадение для страниц, близкое для отдельного листа. Шорох усилили для разборчивости. |
| `book_close.wav` | [BookClose](../../Assets/Resources/GameAudio/BookClose.wav) | Закрытие архивного журнала | Точное совпадение. Смягчили резкий пик закрытия обложки. |

## Алтари и попадания в окружение

| Исходный WAV | Игровой клип | Где звучит | Почему подходит |
| --- | --- | --- | --- |
| `metal_on_stone_1.wav` | [AltarHit](../../Assets/Resources/GameAudio/AltarHit.wav) | Попадание пули в активный кристалл изнутри камеры алтаря | Адаптация. Металлический удар о камень короче звука разрушения печати. |
| `breakstonewall.wav` | [AltarBreak](../../Assets/Resources/GameAudio/AltarBreak.wav) | Разрушение активного кристалла выстрелом или ударом рукоятью | Точное совпадение материала. Хвост осыпи сокращён, чтобы освободить место для реплики. |
| `magic_fizzle.wav` | [AngelDispelled](../../Assets/Resources/GameAudio/AngelDispelled.wav) | Угасание атак Нимба при победе, когда Иван разбил кристалл шестого алтаря и ударил уязвимого босса | Стилизованное совпадение. Затухающий магический звук исходит из позиции Ангела один раз. |
| `metal_on_wood_2.wav` | [WoodImpact](../../Assets/Resources/GameAudio/WoodImpact.wav) | Попадание в дерево и упор деревянной крышки | Совпадение по материалу поверхности. Глухой короткий стук. |
| `weapon_on_metal_2.wav` | [MetalImpact](../../Assets/Resources/GameAudio/MetalImpact.wav) | Попадание в железо и металлическую арматуру | Адаптация. Короткий металлический удар сообщает о твёрдом препятствии. |
| `gravel_on_stone_2.wav` | [WallHit](../../Assets/Resources/GameAudio/WallHit.wav) | Попадание в камень, кладку и прочие твёрдые поверхности | Адаптация. Короткая осыпь отделена от самого выстрела. |
| `weapon_on_earth_1.wav` | [EarthImpact](../../Assets/Resources/GameAudio/EarthImpact.wav) | Попадание в открытый грунт | Адаптация. Мягкий земляной удар соответствует рыхлой поверхности. |

## Локальные источники в окружении

| Исходный WAV | Игровой клип | Где звучит | Почему подходит |
| --- | --- | --- | --- |
| `water_fall2.wav` | [FountainStream](../../Assets/Audio/World/FountainStream.wav) | Фонтан перед церковью, до 11 м | Близкое совпадение. Ровный поток подчёркивает видимые струи. Звук локализован у чаши. |
| `ambiance/fx_addon/drip1.wav` | [CellarDripA](../../Assets/Audio/World/CellarDripA.wav) | Металлическое ведро в подвале церкви | Точное совпадение. Одна короткая капля с затухающим хвостом. Два варианта чередуются с паузами 3,4–8,2 с. |
| `ambiance/fx_addon/drip2.wav` | [CellarDripB](../../Assets/Audio/World/CellarDripB.wav) | То же ведро, вторая капля | Точное совпадение. Отличающийся хвост убирает ощущение метронома. Зону слышимости ограничивают этаж и радиус 4,3 м. |
| `fire_place.wav` | [BrazierCoals](../../Assets/Audio/World/BrazierCoals.wav) | Четыре жаровни с углями в архиве, до 3,35 м | Точное совпадение. Нерегулярный треск очага соответствует углям. У тихой записи поднят уровень. |
| `sfx_torch_11khz_old.wav` | [TorchFlame](../../Assets/Audio/World/TorchFlame.wav) | Пять настенных факелов в архиве, до 2,6 м | Точное совпадение. Более ровное пламя подходит небольшому факелу. Игровая громкость ниже, чем у жаровен. |

Петли воды, факела и углей сведены с перекрытием 100 мс. При паузе, открытом комиксе и выходе из области слышимости источники замолкают. Для свечей и масляных ламп в ритуальном зале сохранён фон сцены. Треск горящих поленьев по масштабу подходит жаровням.

## Шаги по пяти покрытиям

| Исходный WAV | Игровой клип | Где звучит | Почему подходит |
| --- | --- | --- | --- |
| `footstep_shoe_stone_step.wav`, `footstep_shoe_stone_step2.wav`, `footstep_shoe_stone_step3.wav`, `footstep_shoe_stone_step4.wav` | [StoneLeft1](../../Assets/Resources/GameAudio/StoneLeft1.wav), [StoneRight1](../../Assets/Resources/GameAudio/StoneRight1.wav), [StoneLeft2](../../Assets/Resources/GameAudio/StoneLeft2.wav), [StoneRight2](../../Assets/Resources/GameAudio/StoneRight2.wav) | Шаги Ивана, каменный пол, мостовая и твёрдое покрытие | Обувь на соответствующем материале. Чередуются четыре исполнения. |
| `footstep_shoe_gravel_step.wav`, `footstep_shoe_gravel_step2.wav`, `footstep_shoe_gravel_step3.wav`, `footstep_shoe_gravel_step4.wav` | [EarthLeft1](../../Assets/Resources/GameAudio/EarthLeft1.wav), [EarthRight1](../../Assets/Resources/GameAudio/EarthRight1.wav), [EarthLeft2](../../Assets/Resources/GameAudio/EarthLeft2.wav), [EarthRight2](../../Assets/Resources/GameAudio/EarthRight2.wav) | Шаги Ивана, земля и уличный грунт | Близкое совпадение для земли. Зернистый звук смягчён по уровню. Чередуются четыре исполнения. |
| `footstep_shoe_wood_step.wav`, `footstep_shoe_wood_step2.wav`, `footstep_shoe_wood_step3.wav`, `footstep_shoe_wood_step4.wav` | [WoodStep1](../../Assets/Resources/GameAudio/WoodStep1.wav), [WoodStep2](../../Assets/Resources/GameAudio/WoodStep2.wav), [WoodStep3](../../Assets/Resources/GameAudio/WoodStep3.wav), [WoodStep4](../../Assets/Resources/GameAudio/WoodStep4.wav) | Шаги Ивана, деревянная поверхность | Обувь на соответствующем материале. Чередуются четыре исполнения. |
| `footstep_shoe_metal_step.wav`, `footstep_shoe_metal_step2.wav`, `footstep_shoe_metal_step3.wav`, `footstep_shoe_metal_step4.wav` | [MetalStep1](../../Assets/Resources/GameAudio/MetalStep1.wav), [MetalStep2](../../Assets/Resources/GameAudio/MetalStep2.wav), [MetalStep3](../../Assets/Resources/GameAudio/MetalStep3.wav), [MetalStep4](../../Assets/Resources/GameAudio/MetalStep4.wav) | Шаги Ивана, металлическая поверхность | Обувь на соответствующем материале. Чередуются четыре исполнения. |
| `footstep_shoe_carpet_step.wav`, `footstep_shoe_carpet_step2.wav`, `footstep_shoe_carpet_step3.wav`, `footstep_shoe_carpet_step4.wav` | [CarpetStep1](../../Assets/Resources/GameAudio/CarpetStep1.wav), [CarpetStep2](../../Assets/Resources/GameAudio/CarpetStep2.wav), [CarpetStep3](../../Assets/Resources/GameAudio/CarpetStep3.wav), [CarpetStep4](../../Assets/Resources/GameAudio/CarpetStep4.wav) | Шаги Ивана, ковровая дорожка в церкви и ковёр в подвале | Обувь на соответствующем материале. Чередуются четыре исполнения. |

Эти варианты используют Иван и движущаяся охрана. Шаги патрулей и сотрудников оцепления воспроизводятся с половиной усиления шагов Ивана и слышны вблизи.

## Существующий звук

Для стрельбы, перезарядки, тревоги оцепления, захвата, урона персонажам, атак Ангела и подтверждения решения головоломок уже есть игровые записи. У шести архивных рычагов есть свой храповик. Повтор загадки вызывает записанную реплику. Общие фоны улицы и храма, музыка боя, катсцены и озвучка регулируются существующими настройками.
