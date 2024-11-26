#include<iostream>
#include <graphics.h>
#include<conio.h>
ExMessage msg;
char maps[3][3] = {
	{'-','-','-'},
	{'-','-','-'},
	{'-','-','-'}
};
int getxy() {
	while (peekmessage(&msg))
	{
		int screenx=0;
		int screeny=0;
		if (msg.lbutton==1)
		{
			screenx = msg.x;
			screeny = msg.y;
			break;
		}
		if (screenx < 200 && screeny < 200)
			return 1;
		if (screenx < 400 && screeny < 200)
			return 2;
		if (screenx < 600 && screeny < 200)
			return 3;
		if (screenx < 200 && screeny < 400)
			return 4;
		if (screenx < 400 && screeny < 400)
			return 5;
		if (screenx < 600 && screeny < 400)
			return 6;
		if (screenx < 200 && screeny < 600)
			return 7;
		if (screenx < 400 && screeny < 600)
			return 8;
		if (screenx < 600 && screeny < 600)
			return 9;

	}
}
void pric(const int& a) {
	switch (a) {
	case 1:
		circle(100, 100, 100);
		break;
	case 2:
		circle(300, 100, 100);
		break;
	case 3:
		circle(500, 100, 100);
		break;
	case 4:
		circle(100, 300, 100);
		break;
	case 5:
		circle(300, 300,100);
		break;
	case 6:
		circle(500, 300, 100);
		break;
	case 7:
		circle(100, 500, 100);
		break;
	case 8:
		circle(300, 500, 100);
		break;
	case 9:
		circle(500, 500, 100);
		break;
	}
}
void prix(const int &a) {
	switch (a) {
	case 1:
		line(0, 0, 200, 200);
		line(200, 0, 0, 200);
		break;
	case 2:
		line(200, 0, 400, 200);
		line(400, 0, 200, 200);
		break;
	case 3:
		line(400, 0, 600, 200);
		line(600, 0, 400, 200);
		break;
	case 4:
		line(0, 200, 200, 400);
		line(200, 200, 0, 400);
		break;
	case 5:
		line(200, 200, 400, 400);
		line(400, 200, 200, 400);
		break;
	case 6:
		line(400, 200, 600, 400);
		line(600, 200, 400, 400);
			break;
	case 7:
		line(0, 400, 200, 600);
		line(200, 400, 0, 600);
		break;
	case 8:
		line(200, 400, 400, 600);
		line(400, 400, 200, 600);
		break;
	case 9:
		line(400, 400, 600, 600);
		line(600, 400, 400, 600);
		break;

	}
}
bool ifwin(char c) {
	if (maps[1][1] == c && maps[2][1] == c && maps[3][1] == c)
		return true;
	if (maps[1][2] == c && maps[2][2] == c && maps[3][2] == c)
		return true;
	if (maps[1][3] == c && maps[2][3] == c && maps[3][3] == c)
		return true;
	if (maps[1][1] == c && maps[1][2] == c && maps[1][3] == c)
		return true;
	if (maps[2][1] == c && maps[2][2] == c && maps[2][3])
		return true;
	if (maps[3][1] == c && maps[3][2] == c && maps[3][3] == c)
		return true;
	if (maps[1][1] == c && maps[2][2] == c && maps[3][3] == c)
		return true;
	if (maps[3][1] == c && maps[2][2] == c && maps[1][3] == c)
		return true;
	return false;
}
void prmap() {
	line(0, 200, 600, 200);
	line(0, 400, 600, 400);
	line(200, 0, 200, 600);
	line(400, 0, 400, 600);
}
int main()
{
	int i = 1;
	initgraph(600, 600);
	char c;
	while (ifwin==0)
	{
		if (i % 2 == 1) {

		}
		else
		{

		}
		i++;
	}
	_getch();
	return 0;
}
