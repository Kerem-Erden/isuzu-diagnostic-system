idf_component_register(
    SRCS
        "esp32_gateway.c"
        "gateway_protocol.c"
        "can_bus.c"

    INCLUDE_DIRS "."

    PRIV_REQUIRES
        esp_driver_uart
        esp_driver_twai
)