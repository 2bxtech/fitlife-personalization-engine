#!/bin/bash

# Kafka Consumer Test - Verify events are being published
echo "=== Kafka Event Stream Monitor ==="
echo ""
echo "Listening for events on 'user-events' topic..."
echo "Press Ctrl+C to stop"
echo ""

docker exec -it fitlife-kafka kafka-console-consumer \
    --bootstrap-server localhost:9092 \
    --topic user-events \
    --from-beginning \
    --property print.key=true \
    --property print.timestamp=true \
    --property key.separator=" | "
