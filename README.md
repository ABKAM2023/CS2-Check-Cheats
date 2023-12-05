![GitHub Repo stars](https://img.shields.io/github/stars/ABKAM2023/CS2-Check-Cheats?style=for-the-badge)
![GitHub issues](https://img.shields.io/github/issues/ABKAM2023/CS2-Check-Cheats?style=for-the-badge)
![GitHub contributors](https://img.shields.io/github/contributors/ABKAM2023/CS2-Check-Cheats?style=for-the-badge)
![GitHub all releases](https://img.shields.io/github/downloads/ABKAM2023/CS2-Check-Cheats/total?style=for-the-badge)


# CS2-Check-Cheats
Check Cheats - создан для помощи администраторам в проверке подозрительных игроков на использование читов.

**Видео-демонстрация - https://www.youtube.com/watch?v=Uokz0AnyfM4&ab_channel=ABKAM**

# Установка
1. Установите [Metamod:Source](https://www.sourcemm.net/downloads.php/?branch=master) и [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp). Для работы бана [CS2 Admin System](https://csdevs.net/resources/cs2-admin-system.424/)
2. Скачайте Check Cheats
3. Распакуйте архив и загрузите его на игровой сервер

# Как получить доступ к командам?
1. Откройте директорию "addons/counterstrikesharp/configs".
2. Найдите файл "admins.json" в указанной папке.
3. Внутри файла "admins.json" добавьте необходимые флаги для доступа к командам. Например:
```
"76561198847871713": {
    "identity": "76561198847871713",
    "flags": [
        "@admin/check",
        "@admin/uncheck"
    ]
}
```
После этого вам будет предоставлен доступ к командам 

# Основной конфиг (Config.yml)
```
# Конфигурационный файл для плагина CheckCheatsPlugin
# Формат команды для бана игроков (mm_ban {0} время {1})
BanCommand: "mm_ban {0} 0 {1}"
# Причина бана игрока
BanReason: "Cheating"
# Продолжительность проверки игрока в секундах
CheckDuration: "120"
# Формат сообщения обратного отсчета во время проверки игрока
CountdownMessageFormat: "<font color='red' class='fontSize-l'>Вы вызваны на проверку. Скиньте ваш дискорд. Оставшееся время: {remainingTime} сек. Напишите !contact ваш_дискорд.</font>"
# Сообщение об ошибке, когда контактная информация не предоставлена корректно
ErrorMessage: "[{Red}ADMIN{White}] Пожалуйста, укажите ваш дискорд. Используйте: {Green}!contact ваш_дискорд"
# Формат сообщения для администратора после предоставления игроком контактной информации
AdminMessageFormat: "[{Red}ADMIN{White}] Игрок {Yellow}{PlayerName} {White}предоставил свой Дискорд: {Green}{DiscordContact}"
# Сообщение, отображаемое игроку после успешного прохождения проверки
SuccessMessage: "<font color='green' class='fontSize-l'>Вы успешно прошли проверку.</font>"
```

# Команды
- `!check` проверить игрока на читы
- `!uncheck` прекратить проверку игрока
- `!contact` отправить дискорд администратору
