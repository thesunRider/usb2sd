#include <Arduino.h>
#include <SPI.h>
#include <SdFat.h>
#include "USBtoSDAdapter.h"

//PACKET:
// ACK_BIT(D)--COMMAND_START_BIT(D)--ACK_BIT(U)--SUBCMD(D)--CMD_PROPT(D)--ACK(U)--RESP(U)--PAYLOAD(U)--ACK(U)--COMMAND_END_BIT(U)



//SENT: ACK_BIT(D)--COMMAND_START_BIT(D)--                                     --SUBCMD(D)-                -CMD_PROPT(D)--                                        ACK(D)
//RECV:                                     ACK_BIT(U)--COMMAND_START_BIT(U)               ACK_BIT(U)                    --RESP(U)-   ACK_BIT(U)  -PAYLOAD(U)--       --COMMAND_END_BIT(U)


// set up variables using the SD utility library functions:


void USB2SD::debugPrint(char* print) {
  if (DEBUG) {
    Serial.write(DEBUUG_START_BIT);
    Serial.println(print);
    Serial.write(DEBUG_END_BIT);
  }
}

void USB2SD::ClearBuffer() {
  while (Serial.available()) {
    Serial.read();
  }
}

byte USB2SD::waitForSerialByte(unsigned long timeout = 1000) {
  unsigned long startMillis = millis();       // Get the current time
  while (millis() - startMillis < timeout) {  // Wait for the timeout period (1 second)
    if (Serial.available() > 0) {             // Check if data is available in the serial buffer
      return Serial.read();                   // Read and return the byte from the serial buffer
    }
  }
  return 0;  // Return a default value (0) if no data is received within the timeout
}

void USB2SD::init_card() {
  debugPrint("\nInitializing SD card...");
  // we'll use the initialization code from the utility libraries
  // since we're just testing if the card is working!
  if (!sd.begin(PIN_SPI_CS, SPI_FULL_SPEED)) {
    debugPrint("\Card Init Failed ...");
    while (1)
      ;
  } else {
    debugPrint("\nWiring is correct and a card is present.");
  }


  //Serial.print(dir.ls("/"));
}

bool USB2SD::listFiles() {
  File dir;

  if (!dir.open("/")) {
    debugPrint("\nCouldnt open root dir");
    return false;
  }
  // Iterate through the directory
  while (true) {
    File entry = dir.openNextFile();
    if (!entry) {
      // No more files
      break;
    }
    char fileName[100];
    entry.getName(fileName, sizeof(fileName));

    Serial.flush();
    Serial.print(fileName);
    Serial.flush();
    Serial.print(" <#> ");
    Serial.flush();
    Serial.print((uint32_t)entry.fileSize());
    Serial.flush();
    Serial.print(";");
    Serial.flush();
    entry.close();
  }
  return true;
}

void USB2SD::handle_serial() {
  if (Serial.available() >= 2) {
    last_command_success = false;
    if (Serial.read() == ACK_BIT && Serial.read() == COMMAND_START_BIT) {
      Serial.write(ACK_BIT);
      Serial.write(COMMAND_START_BIT);
      byte SUBCMD = waitForSerialByte();
      byte CMD_PROPT;
      if (SUBCMD == SUBCMD_SDCARD_SIZE) {
        CMD_PROPT = waitForSerialByte();                      //dummy data input here
        uint32_t volumesize = sd.vol()->sectorsPerCluster();  // clusters are collections of blocks
        volumesize *= sd.vol()->clusterCount() / 2;           // we'll have a lot of clusters
        Serial.write(ACK_BIT);
        Serial.write(sizeof(volumesize));
        Serial.write(ACK_BIT);
        Serial.write((uint8_t*)&volumesize, sizeof(volumesize));

        //Serial.print(volumesize);
      } else if (SUBCMD == SUBCMD_SDCARD_FREE_SPACE) {
        CMD_PROPT = waitForSerialByte();  //dummy data input here
        uint32_t free_size = sd.vol()->freeClusterCount();
        free_size *= sd.vol()->sectorsPerCluster() / 2;
        Serial.write(ACK_BIT);
        Serial.write(sizeof(free_size));
        Serial.write(ACK_BIT);
        Serial.write((uint8_t*)&free_size, sizeof(free_size));
      } else if (SUBCMD == SUBCMD_SDCARD_FATTYPE) {
        CMD_PROPT = waitForSerialByte();  //dummy data input here
        Serial.write(ACK_BIT);
        Serial.write((int)sd.vol()->fatType());
        Serial.write(ACK_BIT);
        Serial.write(0);  //dummy data output here
      } else if (SUBCMD == SUBCMD_FILES_LIST) {
        CMD_PROPT = waitForSerialByte();  //dummy data input here
        Serial.write(ACK_BIT);
        Serial.write(0);  //we dont know the size
        Serial.write(ACK_BIT);
        listFiles();

      } else if (SUBCMD == SUBCMD_FILES_GET) {
        //GET HAS AN EXTRA PARAMETER SPEEDMODE WHICH COMES Before FILENAME
        CMD_PROPT = waitForSerialByte();
        String filename_in = Serial.readStringUntil("\n");  //get the name of the file to read
        filename_in.trim();

        if (filename_in.length() > 0) {
          File tmp;
          // Open the file from the root directory
          if (tmp.open(filename_in.c_str(), O_READ)) {
            uint32_t flsz = tmp.fileSize();
            Serial.write(ACK_BIT);
            Serial.write((uint8_t*)&flsz, sizeof(flsz));
            Serial.write(ACK_BIT);

            uint8_t byte_read;
            if (CMD_PROPT == 0) {
              while (tmp.available()) {
                byte_read = tmp.read();
                Serial.write(byte_read);  // Write byte to Serial Monitor
                if (Serial.availableForWrite() < 1) Serial.flush();
              }
            } else {
              while (tmp.available()) {
                byte_read = tmp.read();
                Serial.write(byte_read);  // Write byte to Serial Monitor
              }
            }

            tmp.close();  // Close the file after reading
            last_command_success = true;
            Serial.flush();
            Serial.write(COMMAND_END_BIT);
            goto Command_finish;


          } else {
            Serial.write(ACK_BIT);
            Serial.write(10);
            Serial.write(ACK_BIT);
            Serial.write(10);
            last_command_success = false;
            Serial.write(COMMAND_END_BIT);
            goto Command_finish;
          }
        } else {
          Serial.write(ACK_BIT);
          Serial.write(7);
          Serial.write(ACK_BIT);
          Serial.write(7);
          last_command_success = false;
          Serial.write(COMMAND_END_BIT);
          goto Command_finish;
        }

      } else if (SUBCMD == SUBCMD_CHECK_DEV) {
        CMD_PROPT = waitForSerialByte();  //dummy data input here
        Serial.write(ACK_BIT);
        Serial.write(DEV_PRINT);  //we dont know the size
        Serial.write(ACK_BIT);
        Serial.write(DEV_PRINT);
      } else {
        CMD_PROPT = waitForSerialByte();  //dummydatain
        //debugPrint("Invalid Command Request");
        Serial.write(ACK_BIT);
        Serial.write(0x90);  //we dont know the size
        Serial.write(ACK_BIT);
        Serial.write(0x90);
      }
      Serial.flush();
      byte ending_ack = waitForSerialByte();
      last_command_success = (ending_ack == ACK_BIT);
      if (last_command_success) {
        Serial.write(COMMAND_END_BIT);
      } else {
        debugPrint("Invalid Command Completion");
      }
Command_finish:
      //clear rx buffer
      ClearBuffer();

    } else {
      debugPrint("Invalid Stream");
      ClearBuffer();
    }
  }
}

