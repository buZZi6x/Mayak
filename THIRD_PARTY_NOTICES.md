# Источники окружения

## Плачущая девочка: записанный голос и помехи · 14 ноября 2026

Анатомическая основа персонажа, female-young target, скелет и веса:
[MakeHuman Community](https://github.com/makehumancommunity/makehuman),
[core assets CC0](https://github.com/makehumancommunity/makehuman/blob/master/LICENSE.ASSETS.md).
Масштаб, позы и одежда адаптированы для проекта; приложение MakeHuman в игру
не включается.

Волосы **o4saken_long01 — 04saken**, распространены MakeHuman Community в
[Hair01](https://www.makehumancommunity.org/assets/assetpacks/hair01.html),
[CC BY 4.0](https://creativecommons.org/licenses/by/4.0/). Изменения: посадка
на голове, положение нижних прядей над одеждой, деформации головы/шеи и
материал призрака с alpha clipping. Исходная RGBA-текстура сохранена без
изменений. Хотя страница пакета помечена CC0, собственный заголовок
`o4saken_long01.mhclo` указывает CC BY 4.0; проект сохраняет это более строгое
условие и авторство. Точные исходники и лицензии:
`ArtSource/WeepingGirl/SOURCE-LICENSES.md` и `MakeHumanCC0/hair/o4saken_long01`.

| Игровой файл | Автор / запись | Лицензия |
|---|---|---|
| `Girl_Weeping_Recorded.wav` | [AlucardsBride — crying woman.wav](https://freesound.org/people/AlucardsBride/sounds/172738/), полная [OGG-копия Wikimedia Commons](https://commons.wikimedia.org/wiki/File:Cry_of_woman.ogg) | CC0 1.0 |
| `Girl_Static_Dense.wav` | [JovianSounds — Radio Static](https://freesound.org/people/JovianSounds/sounds/524204/), записанный фон рации | CC0 1.0 |

Это реальные голосовая и аппаратная записи. Обработка: обрезка, мягкие фильтры,
регулировка уровня, моно PCM 44,1 кГц и сшивка петли. Высота голоса и темп
не менялись, синтетические форманты и шум не добавлялись. Для Radio Static
использовано доступное авторское HQ MP3-превью полной длительности, не исходный
WAV. Копии страниц с лицензиями, файлы источников, SHA-256 и точные рецепты
находятся в `ArtSource/WeepingGirl/AudioSources/audio-sources.json`.

В исходниках сохранены дополнительные кандидаты CC0: [Radio static — LukaCafuka](https://freesound.org/people/LukaCafuka/sounds/760335/)
и [Girl Crying — mvVoiceActing](https://freesound.org/people/mvVoiceActing/sounds/218184/).
Они не назначены на источники новой встречи. Синтетические звуки первой версии
больше не используются в сцене.

Визуальный эффект помех написан для проекта. Скриншоты Slender в
`ArtSource/WeepingGirl/References` служат только референсом; в игровые ассеты
и сборку они не входят. Из Slender не извлекались код, звуки или текстуры.

## Чайки и звуки маяка · 14 ноября 2026

**Flying seagull — Poly by Google**, [страница модели](https://poly.pizza/m/6Tpj_vcWP3f),
[CC BY 3.0](https://creativecommons.org/licenses/by/3.0/). Модель и исходная текстура
сохранены в `ArtSource/CoastalLife/FlyingSeagull.glb`. Изменения: масштаб в метрах,
позы взмаха и сложенных крыльев, посадочная поза лап и корпуса, серые верхние
поверхности крыльев и тёмные концы, экспорт FBX и материалы HDRP. Редактируемая
производная — `ArtSource/CoastalLife/Seagull.blend`; игровой ассет —
`Lighthouse/Assets/Art/CoastalLife/ResidentGull.prefab`. Указание автора, источника,
лицензии и изменений должно распространяться вместе с игрой; копия этого файла
кладётся рядом с Windows-сборкой.

| Игровой звук | Автор / исходник | Лицензия |
|---|---|---|
| `GullCall02/03/04/06.wav` | Rango Mango, [Solo Seagull Sound Effects](https://opengameart.org/content/solo-seagull-sound-effects), соответствующие Ambient 2/3/4/6 | CC0; автор страницы указывает разрешение на исходную запись |
| `WingTakeoff.wav` | Joseph SARDIN, [Flight Bird #1, 2891](https://bigsoundbank.com/flight-bird-1-s2891.html), шумовое воспроизведение взмахов | CC0 |
| `CameraShutter.wav` | Joseph SARDIN, [Trigger of camera #3, 2544](https://bigsoundbank.com/trigger-of-camera-3-s2544.html), затвор Kodak Six-20 Brownie C | CC0 |
| `LampBurner.ogg` | Joseph SARDIN, [Gas stove #1, 0057](https://bigsoundbank.com/gas-stove-1-s0057.html), тихий участок горения без зажигания | CC0 |
| `RadioStatic.ogg`, `RadioTransmit.wav` | Joseph SARDIN, [Crackling Radio #2, 0313](https://bigsoundbank.com/crackling-radio-2-s0313.html) | CC0 |
| `FlagFlutter.ogg` | Joseph SARDIN, [Flag in the Wind #2, 1633](https://bigsoundbank.com/flag-in-the-wind-2-s1633.html), шумовая запись ткани | CC0 |
| `DoorHinge.wav`, `GalleryCreak.wav`, `LampLever.wav` | rubberduck, [100 CC0 metal and wood SFX](https://opengameart.org/content/100-cc0-metal-and-wood-sfx); уже имеющиеся `wood_squeak_01`, `wood_cracking_03`, `wood_squeak_02` | CC0 |

Страницы и лицензии проверены 14.11.2026. Загруженные оригиналы находятся в
`ArtSource/CoastalLife`, игровые 13 клипов — в
`Lighthouse/Assets/Art/CoastalLife/Audio`. Обработка: моно для пространственных
источников, фильтрация, нормализация с запасом по пикам, короткие фейды;
у трёх циклов сшивка концов. Запись затвора очищена от начальной тишины.
Рецепты обработки сохранены в `Tools/prepare_coastal_audio.ps1`
и `Tools/adapt_coastal_gull.py`. При игре загрузок из интернета нет.

## Звуки разрушения и морских существ · 14 ноября 2026

Исходные записи rubberduck: [100 CC0 metal and wood SFX](https://opengameart.org/content/100-cc0-metal-and-wood-sfx) и [40 CC0 water / splash / slime SFX](https://opengameart.org/content/40-cc0-water-splash-slime-sfx). Обе страницы указывают CC0; ZIP сохранены в `ArtSource/Audio/Marine`. Использованы `wood_squeak_01`, `wood_cracking_01/03/04`, `wood_breaking_01/02`, `wood_slam_01/02/03`, `splash_04/06/09/13/14`, `loop_bubbles_02`.

Игровые производные: `TimberStrain`, `MastBreakA/B`, `HullRupture`, `TentacleImpactA/B`, `HullSubmerging`, `SeaDisplacement`. Замедление, фильтрация, слои с задержками, нормализация и ограничение пиков воспроизводятся `Tools/prepare_marine_audio.ps1`; результат находится в `Lighthouse/Assets/Art/Marine/Audio`. Записи остаются локальными и не требуют сетевого доступа в игре.

Суда V02, глубоководный кракен, их геометрия изломов и процедурная кожа созданы в Blender для этого проекта: `ArtSource/Ocean_Fleet_V02.blend`, генератор `Tools/build_ocean_fleet.py`. Использование процесса GoblinsDungeons не переносит его URP-ассеты, игровые правила или сетевой код в Lighthouse.

Все перечисленные ниже аудиозаписи опубликованы авторами как **CC0**. Страницы источников и лицензии проверены 13 ноября 2026 года. Исходники сохранены в `ArtSource/Audio`, игровые версии — в `Lighthouse/Assets/Environment/Audio`.

| Игровые файлы | Автор и исходник | Источник / лицензия |
|---|---|---|
| `Surf_01–04.ogg` | jasinski, выдержки подготовлены qubodup; Beach Ocean Waves / Alkai Beach | [OpenGameArt](https://opengameart.org/content/beach-ocean-waves), CC0 |
| `Offshore.ogg` | RandomMind; VistulaShort, запись воды на Висле; используется как тихий нижний слой под морским прибоем | [OpenGameArt](https://opengameart.org/content/sea-and-river-wave-sounds), CC0 |
| `Wind.wav` | SketchMan3; wind whoosh loop, производная от Loopable Dungeon Ambience автора JaggedStone | [Wind loop](https://opengameart.org/content/wind-whoosh-loop), [исходная работа](https://opengameart.org/content/loopable-dungeon-ambience), CC0 |
| `RainLight.ogg`, `RainHeavy.ogg` | Ylmir; Rain (loopable), записи 1 и 3 | [OpenGameArt](https://opengameart.org/content/rain-loopable), CC0 |
| `Thunder.wav` | Joseph SARDIN & Axeline T.; Thunder #2, №3113 | [BigSoundBank](https://bigsoundbank.com/thunder-2-s3113.html), CC0 |

Обработка: фильтрация низкочастотного гула, выравнивание громкости с запасом по пикам, преобразование ближнего прибоя в моно для пространственного воспроизведения. Для фоновой воды подготовлен 60-секундный цикл с четырёхсекундной сшивкой. Ветер и дождь используют исходные циклические записи. Гром дополнительно имеет короткие фейды. Код обработки — `Tools/prepare_environment_audio.ps1`.

Небо, облака и спектральная симуляция воды используют **Unity HDRP 17.4.0**, распространяемый по Unity Companion License для проектов Unity. Лицензия пакета доступна в Package Manager и файле `LICENSE.md` установленного пакета. Процедурная карта звёзд, шейдер пены у скалы и погодные настройки созданы для этого проекта.

В готовой игре файлы находятся локально. Для воспроизведения звука и окружения подключение к интернету не требуется.

## Шрифты интерфейса «Маяк» · 14 ноября 2026

- **Cormorant Garamond** — Copyright 2015 The Cormorant Project Authors,
  Christian Thalmann. [Проект автора](https://github.com/CatharsisFonts/Cormorant),
  [файлы Google Fonts](https://github.com/google/fonts/tree/main/ofl/cormorantgaramond).
- **Commissioner** — Copyright 2019 The Commissioner Project Authors,
  Kostas Bartsokas. [Файлы Google Fonts](https://github.com/google/fonts/tree/main/ofl/commissioner).

Оба шрифта получены из официального репозитория Google Fonts и распространяются
по **SIL Open Font License 1.1**. Исходные TTF и полные тексты лицензий сохранены
в `ArtSource/UI/assets/fonts`. Используются локально во всём HTML-интерфейсе;
запросов к Google Fonts при работе меню нет. Оба файла содержат кириллицу.
Для Unity подготовлены статические начертания Commissioner 450/600 и
Cormorant Garamond 550 из этих же variable TTF; исходные оси кроме веса
сохранены по умолчанию. TTF, тексты OFL и динамические SDF FontAsset находятся
в `Lighthouse/Assets/Resources/KeeperMenu/Fonts`. Шрифты работают локально
в меню и игровом HUD.
Предыдущий логотип `ArtSource/UI/assets/mayak-wordmark.svg` использует контуры
Cormorant Garamond 550 с оригинальными деталями маяка и кракена. Исходный
шрифт и его лицензия сохранены рядом с остальными UI-ассетами.
Актуальный `ArtSource/UI/assets/mayak-wordmark-v2.svg` нарисован собственными
векторными контурами; готовый шрифт и сторонние изображения в нём не использованы.
Та же версия находится в `Lighthouse/Assets/Resources/KeeperMenu/mayak-wordmark.svg`;
маски и clip path для этого рисунка не требуются.

## Музыка главного меню

Музыка главного меню **Deckside Jig** предоставлена владельцем проекта
14.11.2026 файлом `C:/Users/Vlad/Downloads/Deckside Jig.mp3` с прямым поручением
добавить её в меню и зациклить. Копия без изменения исходных байтов:
`Lighthouse/Assets/Audio/Menu/DecksideJig.mp3`. Авторство и публичная лицензия
из этого файла не устанавливались; материал не обозначается как CC0 или OFL.

## Радио смотрителя

Три полных трека **Ashes of the Keep**, **Deckside Jig**, **Sun-Faded Memory**
предоставлены владельцем проекта 14.11.2026 для случайного воспроизведения в радио.
Исходные MP3 сохранены в `ArtSource/Radio/Music`; игровые OGG содержат только
звук, с выравниванием громкости до −20 LUFS / −2 dBTP. Происхождение и контрольные
суммы записаны в `ArtSource/Radio/music-manifest.json`. Публичная лицензия
для этих предоставленных материалов не назначалась.

24 оригинальных русских текста написаны для проекта и синтезированы локальным
голосом Microsoft Pavel, затем обработаны как узкополосный радиоэфир.
Это синтетическая речь. Тексты, исходные WAV и рецепт обработки находятся
в `ArtSource/Radio`; помехи и эмбиент процедурно созданы для проекта.
Рисунок бирюзового приёмника создан ImageGen по контексту игры и согласован
владельцем. Концепт: `ArtSource/UI/Concepts/Radio/2026-11-14`; подготовленная
под управление основа: `ArtSource/UI/assets/radio/receiver-plate.png`.

## Голос левиафана

**Cthulhu growl.wav** — [cylon8472, Freesound](https://freesound.org/people/cylon8472/sounds/249686/),
[Creative Commons Attribution 4.0 International](https://creativecommons.org/licenses/by/4.0/).
Автор описывает запись собственного рычания с последующей обработкой в Audacity;
источник опубликован 23 сентября 2014 года. Использована общедоступная HQ MP3-копия.

Изменения для игры: понижение высоты на 15%, эквализация и фильтрация,
плавные начало и хвост, нормализация громкости, преобразование в mono WAV 48 kHz.
Результат — `Assets/Art/Leviathan/Audio/Leviathan_Roar.wav`.
Исходная запись, сохранённая страница, SHA-256 и рецепт обработки:
`ArtSource/Leviathan/AudioSources/`. При передаче сборки включать этот файл
с указанием автора, источника, лицензии и изменений.

## Материалы маяка

Три набора PBR-карт взяты у Poly Haven, лицензия [CC0](https://polyhaven.com/license), проверено 13 ноября 2026 года:

- [Weathered Planks](https://polyhaven.com/a/weathered_planks) — Dimitrios Savva и Dario Barresi: дерево галереи, лестницы и мебели.
- [White Plaster Rough 01](https://polyhaven.com/a/white_plaster_rough_01) — Rob Tuytel: штукатурка башни.
- [Rock Face](https://polyhaven.com/a/rock_face) — Greg Zaal и Dario Barresi: поверхность скалы.

Исходные PNG и ссылки с контрольными суммами находятся в `ArtSource/Surfaces`. `Tools/fetch_coastal_surfaces.py` загружает карты из публичного API и проверяет MD5. В Blender карты собраны в материалы: цвет камня охлаждён, дерево слегка тонировано, ARM преобразован в HDRP Mask (metallic / AO / detail / smoothness). Другие материалы и вся геометрия маяка, скалы, интерьера и камеры созданы в Blender для проекта. Текстуры упакованы в `.blend`.
