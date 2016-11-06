docker run -ti -e BYN-EXCHANGE-BOT-API-KEY='' shchegrikovich/byn-exchange-bot
docker run -d -e BYN-EXCHANGE-BOT-API-KEY='' --restart=unless-stopped --log-driver=syslog shchegrikovich/byn-exchange-bot

Docker container - https://hub.docker.com/r/shchegrikovich/byn-exchange-bot/

Telegram bot - https://telegram.me/byn_exchange_bot