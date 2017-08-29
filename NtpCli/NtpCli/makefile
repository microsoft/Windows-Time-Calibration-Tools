TARGET = ntpcli

$(TARGET): ntpcli.o $(LIB)
	g++ $^ -o $(TARGET) -lpthread -O3

ntpcli.o: ntpcli.cpp
	g++ -c $^ -o $@ -std=c++14 

clean:
	rm -f *.o $(TARGET)

