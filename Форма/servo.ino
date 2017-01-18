#include <Servo.h>

Servo servo1;
Servo servo2;
Servo servo3;
Servo servo4;
Servo servo5;

void setup()
{
  Serial.begin(57600);
}

void loop() 
{
  if( Serial.available() > 0 )
  {
    int numServo = Serial.read();
    int numPin = Serial.read();
    int angle = Serial.read();
    Servo* servo;
    if( numServo >= 1 && numServo <= 5 && numPin >= 0 && numPin <= 32 )
    {
      switch(numServo)
      {
        case 1: servo = &servo1; break;
        case 2: servo = &servo2; break;
        case 3: servo = &servo3; break;
        case 4: servo = &servo4; break;
        case 5: servo = &servo5; break;
      }
      servo->attach(numPin);
      servo->write(angle);
    }
  }
  else
    delay(500);
}

