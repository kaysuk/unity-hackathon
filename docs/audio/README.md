# Черновой звук «Братьев Карамазовых»

[Послушать монтаж, 32 секунды](temple-sound-preview.wav). В начале Иван переходит с земли на камень. На 0:07 охрана замечает его, на 0:09 слышна ответная очередь. С 0:15 звучат бумага, ключ и решётка. С 0:22 звучат ранние варианты двух атак Ангела. Монтаж собран из игровых клипов вручную. В игре расстояние, положение камеры и акустика комнаты меняют баланс.

| Событие | Что слышно | Клипы для замены |
| --- | --- | --- |
| Ходьба, бег, присед, разворот, приземление | Четыре чередующихся удара подошвы на каждом покрытии. На земле слышно зерно под ногой, на камне заметнее удар пятки. Одежда звучит отдельным слоем. При движении в приседе оба слоя тише. | `EarthLeft1…EarthRight2`, `StoneLeft1…StoneRight2`, `Cloth`, `Scuff`, `Land` |
| Достать оружие, прицелиться, убрать, сменить магазин | Трение кобуры, движение ткани, два разных металлических щелчка. Если перезарядка сорвалась, завершающий щелчок отсутствует. У пустого пистолета слышен сухой спуск. | `Draw`, `Stow`, `Cloth`, `MagazineOut`, `MagazineIn`, `DryFire` |
| Стрельба и попадания | Одиночный выстрел Ивана и отдельная запись винтовки. Каждый выстрел очереди запускает сама охрана. Попадание в стену даёт короткую осыпь в точке удара. | `Pistol`, `Rifle shot`, `WallHit` |
| Захват сзади, ранение и падение | Рывок ткани в начале захвата, короткая голосовая реакция при уроне, затем тяжёлый удар тела при гибели. | `Grapple`, `BodyHit`, `BodyFall` |
| Обнаружение охраной и тревога у церкви | Сигнал из точки обнаружения, обрамлённый щелчками связи. У группы общий трёхсекундный интервал, чтобы один контакт не звучал хором. | `SWAT alert` |
| Документы, ключи, двери и решётки | Лист шуршит при чтении и перелистывании. Ключ и защёлка звучат отдельно. У движущейся решётки есть старт и удар при остановке. | `Paper`, `Key`, `Door`, `Gate`, `GateStop` |
| Руны, плиты, рычаги и печати | Камень садится в гнездо, диск и плитка скребут, храповик проворачивается. Ошибка печати глухая, решение заканчивается бронзовым созвучием. | `Stone`, `Slide`, `Lever`, `Archive ratchet`, `Switch`, `Seal`, `SealFail` |
| Нимб | У каждой метки звенят колокольчики, у вспышки звучит удар. Захват нимба предупреждает о волне, при выпуске слышен раскат. Замах перед рывком сопровождает низкий порыв, подъём руки — тонкий звон, вылет каждого копья — короткий импульс. | `AngelCharge`, `AngelStrike`, `HaloGrip`, `HaloWave`, `HeadDash`, `NimbHandChime`, `LightSpear` |
| Меню и музыка | Короткий механический щелчок кнопок. В меню звучит тёмный фон. Улица, церковь и храм имеют свои петли. Музыка боя начинается на кадре 30 и продолжается в финальном зале. | `MenuClick`, `Undercroft`, `church_exterior`, `church_interior`, `temple`, `boss_fight` |

## Исходные записи

| Материал | Авторы и использованные записи |
| --- | --- |
| Выстрелы | Ben Jaszczak, Brian Nelson, Kevin Heras, Matthew Nanney. Дубли 1911 `A_34P` и AR-15 `D_24P`, вырезан один выстрел с естественным хвостом. [The Free Firearm Sound Library](https://opengameart.org/content/the-free-firearm-sound-library) · [Зеркало исходных WAV](https://github.com/petroulacl/fps-asset-kit/tree/main/sfx/firearm_sfx) |
| Подошвы | Fantozzi, подборка qubodup. Левые и правые шаги по камню и песку. [Fantozzi’s Footsteps](https://opengameart.org/content/fantozzis-footsteps-grasssand-stone) |
| Ткань, предметы, дверь и голос существа | artisticdude. Слои из разделов inventory, world, battle и NPC/giant. [RPG Sound Pack](https://opengameart.org/content/rpg-sound-pack) |
| Металл и механизмы | Brian MacIntosh (BMacZero). Использованы clank1, lightclunk1, rattle1, squeakyclick2 и mechanical1. [Mechanical Sounds](https://opengameart.org/content/mechanical-sounds) |
| Ранения | EZduzziteh. Дубли hurt_01 и hurt_05. [Hurt Sound Effects](https://opengameart.org/content/hurt-sound-effects) |
| Бумага | yd. FlippingPages.ogg. [Old Pages/Flip with sound](https://opengameart.org/content/old-pagesflip-with-sound) |
| Руническая метка | zazz.sound.design. Первые 0,63 с россыпи колокольчиков с затуханием. [Magic Spell Fairy Dust](https://freesound.org/people/zazz.sound.design/sounds/683323/) |
| Световая волна | qubodup. [Shockwave](https://freesound.org/people/qubodup/sounds/814053/) |
| Рывок | Qvantor. [Whoosh Deep Heavy](https://freesound.org/people/Qvantor/sounds/877977/) |
| Выпад рукой | PWL. Файл `bell_ding1.wav`. [Bell dings/chimes](https://opengameart.org/content/bell-dingschimes) |
| Залп копий | EZduzziteh. Файл `sfx_laser_3.mp3`. [Laser Sound Effects](https://opengameart.org/content/laser-sound-effects) |
| Фоновая музыка | congusbongus. Для исследования использован полный авторский цикл. [Lost in a bad place](https://opengameart.org/content/lost-in-a-bad-place-horror-ambience-loop) |

Пять выбранных эффектов Нимба опубликованы по [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/). В игру перенесены те же версии, что и в каталоге прослушивания, в моно. Для трёх записей Freesound источником служили HQ MP3-превью.

Сигнал обнаружения сохраняет исходную запись проекта «SWAT alert». Исходная музыка ритуала сохранена в банке прототипа.

## Музыка по сценам

Игровые WAV лежат в `Assets/Resources/sounds`, изображения — в `Assets/Resources/comics`. Номер в имени изображения соответствует кадру сценария.

| Кадры или локация | Запись |
| --- | --- |
| 5–6, самолёт | `comics/comics5-6.wav` |
| 7–14, семья и исчезновение отца | `comics/comics_7-14.wav` |
| Улица у церкви, оба посещения | `ambience/church_exterior.wav` |
| Внутри церкви | `ambience/church_interior.wav` |
| Первый и нижний подземные храмы | `ambience/temple.wav` |
| 15–19, ритуал и бегство | `comics/comics15-19.wav` |
| 20–29, офис и выбор судьбы Дмитрия | `comics/comics20-29.wav` |
| 30–33 и весь бой | `ambience/boss_fight.wav` |
| 34–38, плохая, хорошая и лучшая концовки | `comics/comics34-38_bad.wav`, `comics/comics34-38_good.wav`, `comics/comics34-38_best.wav` |

Первые два кадра показываются по семь секунд. Следующие страницы завершаются после записанных для них реплик. Пробел, A на геймпаде или щелчок мыши переводят на следующую страницу. Кадр 28 ждёт решения игрока. На кадре 29 изображение отражает выбор. При здоровье 30% и ниже у живого Ивана звучит `player_sounds/player_heartb.wav`.

Подробная раскладка актёрских голосов и их привязок приведена в [каталоге озвучки](speech.md).

## Замена на заказную озвучку

Основной банк находится в `Assets/Resources/GameAudio`. Звон руки Нимба находится в `Assets/Resources/NimbHandChime.wav`. Записи винтовки, сигнала обнаружения и архивного храповика лежат в `Assets/Audio`. Для замены достаточно обновить содержимое одноимённого файла и сохранить его Unity `.meta`. Игровые события и ссылки префабов продолжат работать.

Эффекты записаны в моно, музыка — в стерео. В главном меню можно настроить общую громкость и громкость музыки, речи и эффектов. У мировых звуков есть позиция и затухание. В храме используется короткая каменная реверберация, в ритуальном зале — отражения большего помещения. Музыка и звуки интерфейса обходят эти зоны.
