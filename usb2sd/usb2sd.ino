
#include "USBtoSDAdapter.h"
#include <SPI.h>
#include <SdFat.h> //Install SDFat

#define PIN_SPI_CS 4 //set this according to your micro sdcard

USB2SD usb2sd;

void setup() {
  // put your setup code here, to run once:
  Serial.begin(default_baudrate); //2M baud
   while (!Serial) {
    ;  // wait for serial port to connect. Needed for native USB port only
  }
  usb2sd.init_card();

}

void loop() {
  // put your main code here, to run repeatedly:
   usb2sd.handle_serial();
}
