// functions.h
#ifndef USB2SD_H
#define USB2SD_H

#include "Arduino.h"

#include <SPI.h>
#include <SdFat.h>

#define DEBUG 1

#define default_baudrate 2000000

#define ACK_BIT 0x20
#define COMMAND_START_BIT 0x21
#define COMMAND_END_BIT 0x22

#define SUBCMD_SDCARD_SIZE 0x1
#define SUBCMD_SDCARD_FREE_SPACE 0x2
#define SUBCMD_SDCARD_FATTYPE 0x3
#define SUBCMD_FILES_LIST 0x4
#define SUBCMD_FILES_GET 0x5
#define SUBCMD_CHECK_DEV 0x6

#define DEBUUG_START_BIT 0x19
#define DEBUG_END_BIT 0x21
#define DEV_PRINT 0xBF

//PACKET:
// ACK_BIT(D)--COMMAND_START_BIT(D)--ACK_BIT(U)--SUBCMD(D)--CMD_PROPT(D)--ACK(U)--RESP(U)--PAYLOAD(U)--ACK(U)--COMMAND_END_BIT(U)



//SENT: ACK_BIT(D)--COMMAND_START_BIT(D)--                                     --SUBCMD(D)-                -CMD_PROPT(D)--                                        ACK(D)
//RECV:                                     ACK_BIT(U)--COMMAND_START_BIT(U)               ACK_BIT(U)                    --RESP(U)-   ACK_BIT(U)  -PAYLOAD(U)--       --COMMAND_END_BIT(U)


// set up variables using the SD utility library functions:

class USB2SD {
public:
  SdFat sd;
  void debugPrint(char* print);
  void ClearBuffer() ;
  byte waitForSerialByte(unsigned long timeout = 1000);
  void init_card();
  bool listFiles();
  void handle_serial();

private:
  boolean last_command_success = true;


};
#endif