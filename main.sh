#!/bin/bash

echo "Starting program..."

/usr/local/bin/python /app/automated_run.py /app/tracking.yaml

echo "Job finished."
echo "Sleeping until next run."
