#!/bin/sh

touch /var/log/main.log

# Configure cron job
echo "${CRON_SCHEDULE} /app/main.sh >> /var/log/main.log 2>&1" | crontab -

# Use hcron to display the cron schedule in a human-readable format
# https://github.com/lnquy/cron
NEXT_EXECUTION=$(/app/hcron -24-hour "${CRON_SCHEDULE}")
echo "Cron configured to run with $NEXT_EXECUTION"

# Start cron
cron

tail -f /var/log/main.log