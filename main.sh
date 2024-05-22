#!/bin/bash

echo "Starting program at $(date +"%H:%M")..."

/usr/local/bin/python /app/automated_run.py /app/tracking.yaml

echo "Job finished at $(date +"%H:%M")."
echo "Sleeping until next run."
